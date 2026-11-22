it /**
 * @file main.c
 * @brief Firmware dual-core para Frecuencímetro y Generador de Señales PWM en Raspberry Pi Pico (RP2040).
 *
 * Arquitectura del sistema (reloj del sistema ajustado a 240 MHz):
 *  - Core 0: Gestión de comunicación serie (USB-CDC), decodificación de comandos de control
 *            y generación híbrida de señales PWM (0,01 Hz a 120 MHz).
 *  - Core 1: Adquisición continua y envío de telemetría por USB-CDC mediante selección
 *            automática de método (conteo hardware en ventana para >= 10 kHz y captura
 *            recíproca de flancos para < 10 kHz).
 */

#include <stdio.h>           // printf, sscanf, getchar_timeout_us
#include <stdlib.h>          // Funciones estándar de conversión
#include "pico/stdlib.h"     // stdio_init_all, sleep_ms, sleep_us, time_us_64
#include "pico/multicore.h"  // multicore_launch_core1
#include "hardware/clocks.h" // set_sys_clock_khz
#include "hardware/vreg.h"   // vreg_set_voltage (necesario para overclock estable a 240 MHz)
#include "hardware/pwm.h"    // Configuración de slices, contadores y wrapping PWM
#include "hardware/irq.h"    // Gestión de rutinas de interrupción (PWM_IRQ_WRAP)
#include "hardware/sync.h"   // Secciones críticas atómicas (save_and_disable_interrupts)

// ============================================================================
// DEFINICIÓN DE PINES HARDWARE
// ============================================================================
#define PIN_GEN  16 // Salida del generador de señal (Slice 0, Canal A)
#define PIN_MED  15 // Entrada del medidor de frecuencia / flancos ascendentes (Slice 7, Canal B)
#define PIN_DUTY 13 // Entrada para medición hardware de nivel alto (Slice 6, Canal B - puenteada con PIN_MED)
#define LED_PIN  25 // LED integrado en placa para indicación visual de actividad y comandos

// ============================================================================
// VARIABLES GLOBALES Y SINCRONIZACIÓN ENTRE NÚCLEOS
// ============================================================================

// Variables para extender los contadores PWM hardware de 16 bits a 32 bits mediante IRQ
static uint pwm_slice_med = 0;                       // Índice del slice PWM asignado a PIN_MED
static uint pwm_slice_duty = 0;                      // Índice del slice PWM asignado a PIN_DUTY
static volatile uint32_t pwm_overflow_count = 0;     // Desbordamientos (wraps) del contador de flancos
static volatile uint32_t duty_overflow_count = 0;    // Desbordamientos (wraps) del contador de ciclo alto

// Variables de control del contador acumulado de eventos (compartidas Core 0 <-> Core 1)
static volatile uint64_t total_eventos = 0;          // Total acumulado de flancos ascendentes detectados
static volatile bool reset_eventos_req = false;      // Bandera de solicitud de puesta a cero desde Core 0
static volatile bool contador_pausado = false;       // Bandera de pausa/reanudación del conteo acumulado

// Variables de estado para el generador por timer hardware en bajas frecuencias (< 16 Hz)
static struct repeating_timer timer_bf;              // Estructura del temporizador repetitivo de 64 bits
static bool timer_bf_activo = false;                 // Indica si el temporizador de baja frecuencia está en marcha
static uint64_t bf_t_alto_us = 500000;               // Duración del semiperiodo en nivel ALTO (microsegundos)
static uint64_t bf_t_bajo_us = 500000;               // Duración del semiperiodo en nivel BAJO (microsegundos)
static bool bf_estado_pin = false;                   // Estado lógico actual del pin de salida en modo timer

// ============================================================================
// RUTINAS DE INTERRUPCIÓN (ISR) Y LECTURA ATÓMICA DE CONTADORES
// ============================================================================

/**
 * @brief Callback del temporizador hardware para generación de señales de baja frecuencia (< 16 Hz).
 * Conmuta manualmente el estado de PIN_GEN y reprograma el retardo del siguiente semiperiodo.
 */
bool timer_frec_bajas(struct repeating_timer *t) {
    if (bf_t_alto_us == 0 || bf_t_bajo_us == 0) {
        return false; // Detiene el temporizador si el duty cycle es extremo (0 % o 100 %)
    }

    if (bf_estado_pin) {
        gpio_put(PIN_GEN, 0);
        bf_estado_pin = false;
        t->delay_us = -(int64_t)bf_t_bajo_us; // Valor negativo = intervalo exacto inicio-a-inicio
    } else {
        gpio_put(PIN_GEN, 1);
        bf_estado_pin = true;
        t->delay_us = -(int64_t)bf_t_alto_us;
    }
    return true; // Mantiene activo el temporizador repetitivo
}

/**
 * @brief Rutina de servicio de interrupción (ISR) para desbordamiento de contadores PWM (0xFFFF -> 0x0000).
 * Extiende la resolución efectiva de ambos slices de medición de 16 a 32 bits.
 */
void on_pwm_wrap(void) {
    uint32_t mask = pwm_get_irq_status_mask();
    
    if (mask & (1u << pwm_slice_med)) {
        pwm_clear_irq(pwm_slice_med);
        pwm_overflow_count++;
    }
    if (mask & (1u << pwm_slice_duty)) {
        pwm_clear_irq(pwm_slice_duty);
        duty_overflow_count++;
    }
}

/**
 * @brief Lee de forma atómica el contador de 32 bits de flancos ascendentes (PIN_MED).
 * Deshabilita temporalmente las interrupciones para evitar condiciones de carrera si ocurre
 * un desbordamiento justo entre la lectura de los 16 bits altos y los 16 bits bajos.
 */
static uint32_t leer_contador_continuo(void) {
    uint32_t flags = save_and_disable_interrupts();
    uint32_t overflows = pwm_overflow_count;

    // Comprueba si quedó una interrupción pendiente justo antes de deshabilitar IRQs
    if (pwm_get_irq_status_mask() & (1u << pwm_slice_med)) {
        pwm_clear_irq(pwm_slice_med);
        pwm_overflow_count++;
        overflows = pwm_overflow_count;
    }

    uint16_t cuenta_raw = pwm_get_counter(pwm_slice_med);
    restore_interrupts(flags);

    return (overflows << 16) | cuenta_raw;
}

/**
 * @brief Lee de forma atómica el contador de 32 bits de ciclos en nivel ALTO (PIN_DUTY).
 */
static uint32_t leer_contador_duty(void) {
    uint32_t flags = save_and_disable_interrupts();
    uint32_t overflows = duty_overflow_count;

    if (pwm_get_irq_status_mask() & (1u << pwm_slice_duty)) {
        pwm_clear_irq(pwm_slice_duty);
        duty_overflow_count++;
        overflows = duty_overflow_count;
    }

    uint16_t cuenta_raw = pwm_get_counter(pwm_slice_duty);
    restore_interrupts(flags);

    return (overflows << 16) | cuenta_raw;
}

// ============================================================================
// FUNCIONES DE MEDICIÓN Y GENERACIÓN DE SEÑALES
// ============================================================================

/**
 * @brief Mide por captura directa de flancos el tiempo en ALTO y en BAJO de un ciclo completo.
 * Utilizado para frecuencias bajas (< 10 kHz) donde el conteo en ventana de 100 ms pierde resolución.
 *
 * @param pin        GPIO de entrada a muestrear.
 * @param timeout_us Tiempo máximo de espera antes de declarar ausencia de señal.
 * @param t_alto     Puntero donde se almacena la duración en nivel alto (us).
 * @param t_bajo     Puntero donde se almacena la duración en nivel bajo (us).
 * @return true si se capturó un ciclo válido; false si expiró el timeout.
 */
static bool medir_tiempos_us(uint pin, uint32_t timeout_us, uint64_t *t_alto, uint64_t *t_bajo) {
    uint64_t limite = time_us_64() + timeout_us;

    // Sincronización con el inicio de un pulso completo (flanco de subida limpio)
    while (gpio_get(pin))  { if (time_us_64() > limite) return false; } // Espera a que termine un nivel alto previo
    while (!gpio_get(pin)) { if (time_us_64() > limite) return false; } // Espera flanco de subida (inicio de pulso)
    uint64_t t1 = time_us_64();

    while (gpio_get(pin))  { if (time_us_64() > limite) return false; } // Espera flanco de bajada (fin de nivel alto)
    uint64_t t2 = time_us_64();

    while (!gpio_get(pin)) { if (time_us_64() > limite) return false; } // Espera siguiente flanco de subida (fin de ciclo)
    uint64_t t3 = time_us_64();

    *t_alto = t2 - t1;
    *t_bajo = t3 - t2;
    return true;
}

/**
 * @brief Configura el generador híbrido de señal PWM (frecuencia y ciclo de trabajo).
 * Selecciona automáticamente entre temporizador por software/IRQ (< 16 Hz) o bloque PWM
 * hardware puro (>= 16 Hz) con ajuste dinámico del divisor de reloj.
 *
 * @param pin            GPIO de salida (PIN_GEN).
 * @param frec_target_hz Frecuencia deseada en Hz (0 para apagar salida).
 * @param duty_percent   Ciclo de trabajo deseado (0.0 a 100.0 %).
 * @return Frecuencia real generada tras cuantización de registros hardware.
 */
double set_pwm_output(uint pin, double frec_target_hz, double duty_percent) {
    uint slice_num = pwm_gpio_to_slice_num(pin);
    uint chan = pwm_gpio_to_channel(pin);

    // Detiene cualquier temporizador de baja frecuencia previo antes de reconfigurar el pin
    if (timer_bf_activo) {
        cancel_repeating_timer(&timer_bf);
        timer_bf_activo = false;
    }

    // Si la frecuencia es <= 0 Hz, apaga la salida y la fuerza a estado bajo seguro (0 V)
    if (frec_target_hz <= 0.0) {
        pwm_set_enabled(slice_num, false);
        gpio_set_function(pin, GPIO_FUNC_SIO);
        gpio_set_dir(pin, GPIO_OUT);
        gpio_put(pin, 0);
        return 0.0;
    }

    // Saturación de seguridad para el ciclo de trabajo
    if (duty_percent < 0.0) duty_percent = 0.0;
    if (duty_percent > 100.0) duty_percent = 100.0;

    // ------------------------------------------------------------------------
    // MODO 1: BAJA FRECUENCIA (< 16 Hz) -> Temporizador hardware de 64 bits
    // (El contador PWM de 16 bits con clkdiv máx. 256 no alcanza < 14,3 Hz a 240 MHz)
    // ------------------------------------------------------------------------
    if (frec_target_hz < 16.0) {
        pwm_set_enabled(slice_num, false);
        gpio_set_function(pin, GPIO_FUNC_SIO);
        gpio_set_dir(pin, GPIO_OUT);

        double periodo_us = 1000000.0 / frec_target_hz;
        uint64_t alto = (uint64_t)(periodo_us * (duty_percent / 100.0) + 0.5);
        uint64_t bajo = (alto >= (uint64_t)periodo_us) ? 0 : ((uint64_t)periodo_us - alto);

        // Si el pulso es menor de 50 us en un extremo, se fija nivel DC sin lanzar IRQs
        if (alto < 50) {
            gpio_put(pin, 0);
            return frec_target_hz;
        }
        if (bajo < 50) {
            gpio_put(pin, 1);
            return frec_target_hz;
        }

        bf_t_alto_us = alto;
        bf_t_bajo_us = bajo;
        bf_estado_pin = true;
        gpio_put(pin, 1);

        if (add_repeating_timer_us(-(int64_t)bf_t_alto_us, timer_frec_bajas, NULL, &timer_bf)) {
            timer_bf_activo = true;
        }

        return frec_target_hz;
    }

    // ------------------------------------------------------------------------
    // MODO 2: MEDIA Y ALTA FRECUENCIA (>= 16 Hz) -> Periférico PWM Hardware puro
    // ------------------------------------------------------------------------
    gpio_set_function(pin, GPIO_FUNC_PWM);

    double f_sys = 240000000.0; // Reloj del sistema a 240 MHz
    float clkdiv = 1.0f;

    // Prescaler de 250 para cubrir de 16 Hz a 4 kHz sin desbordar el registro TOP de 16 bits
    if (frec_target_hz < 4000.0) clkdiv = 250.0f;

    // Cálculo de TOP (wrap) y nivel de comparación (CC)
    double wrap_exacto = (f_sys / (clkdiv * frec_target_hz)) - 1.0;
    uint32_t wrap = (uint32_t)(wrap_exacto + 0.5);

    if (wrap < 1) wrap = 1;
    if (wrap > 65535) wrap = 65535;

    uint32_t level = (uint32_t)(((double)(wrap + 1) * (duty_percent / 100.0)) + 0.5);
    if (level > 65535) level = 65535;

    double frec_real = f_sys / (clkdiv * (double)(wrap + 1));

    // Aplicación de parámetros al slice PWM
    pwm_set_enabled(slice_num, false);
    pwm_set_clkdiv(slice_num, clkdiv);
    pwm_set_wrap(slice_num, (uint16_t)wrap);
    pwm_set_chan_level(slice_num, chan, (uint16_t)level);
    pwm_set_enabled(slice_num, true);

    return frec_real;
}

// ============================================================================
// CORE 1: ADQUISICIÓN HARDWARE CONTINUA Y TRANSMISIÓN DE TELEMETRÍA
// ============================================================================

/**
 * @brief Punto de entrada del segundo núcleo (Core 1).
 * Configura dos slices PWM independientes en modo contador externo (Canal B):
 *  - Slice de PIN_MED: Incrementa en cada flanco ascendente de entrada (PWM_DIV_B_RISING).
 *  - Slice de PIN_DUTY: Incrementa a 240 MHz mientras la entrada esté en nivel ALTO (PWM_DIV_B_HIGH).
 */
void core1_entry(void) {
    // 1. Configuración de PIN_MED como contador de flancos ascendentes
    assert(pwm_gpio_to_channel(PIN_MED) == PWM_CHAN_B);
    pwm_slice_med = pwm_gpio_to_slice_num(PIN_MED);
    gpio_set_function(PIN_MED, GPIO_FUNC_PWM);

    pwm_config cfg = pwm_get_default_config();
    pwm_config_set_clkdiv_mode(&cfg, PWM_DIV_B_RISING);
    pwm_config_set_clkdiv(&cfg, 1.0f);
    pwm_config_set_wrap(&cfg, 0xFFFF);
    pwm_init(pwm_slice_med, &cfg, false);

    // 2. Configuración de PIN_DUTY como contador de tiempo en nivel ALTO (240 MHz)
    assert(pwm_gpio_to_channel(PIN_DUTY) == PWM_CHAN_B);
    pwm_slice_duty = pwm_gpio_to_slice_num(PIN_DUTY);
    gpio_set_function(PIN_DUTY, GPIO_FUNC_PWM);

    pwm_config cfg_duty = pwm_get_default_config();
    pwm_config_set_clkdiv_mode(&cfg_duty, PWM_DIV_B_HIGH);
    pwm_config_set_clkdiv(&cfg_duty, 1.0f);
    pwm_config_set_wrap(&cfg_duty, 0xFFFF);
    pwm_init(pwm_slice_duty, &cfg_duty, false);

    // 3. Habilitación de interrupciones de desbordamiento para ambos slices
    pwm_clear_irq(pwm_slice_med);
    pwm_clear_irq(pwm_slice_duty);
    pwm_set_irq_enabled(pwm_slice_med, true);
    pwm_set_irq_enabled(pwm_slice_duty, true);
    irq_set_exclusive_handler(PWM_IRQ_WRAP, on_pwm_wrap);
    irq_set_enabled(PWM_IRQ_WRAP, true);

    // 4. Puesta a cero y arranque simultáneo de ambos contadores
    pwm_set_counter(pwm_slice_med, 0);
    pwm_set_counter(pwm_slice_duty, 0);
    pwm_overflow_count = 0;
    duty_overflow_count = 0;
    pwm_set_enabled(pwm_slice_med, true);
    pwm_set_enabled(pwm_slice_duty, true);

    uint32_t lectura_previa = 0;

    while (true) {
        // Atiende solicitud de reinicio del acumulador de eventos
        if (reset_eventos_req) {
            lectura_previa = leer_contador_continuo();
            total_eventos = 0;
            reset_eventos_req = false;
        }

        // --------------------------------------------------------------------
        // FASE 1: Ventana de muestreo hardware de 100 ms
        // --------------------------------------------------------------------
        uint32_t c_inicio = leer_contador_continuo();
        uint32_t d_inicio = leer_contador_duty();
        uint64_t t_inicio = time_us_64();
        
        sleep_us(100000); // Ventana de integración de 100 ms
        
        uint32_t c_fin = leer_contador_continuo();
        uint32_t d_fin = leer_contador_duty();
        uint64_t t_fin = time_us_64();

        // --------------------------------------------------------------------
        // FASE 2: Cálculo de pulsos, acumulación de eventos y estimación inicial
        // --------------------------------------------------------------------
        uint32_t pulsos_ventana = c_fin - c_inicio;
        uint32_t ciclos_alto = d_fin - d_inicio;
        uint32_t pulsos_ciclo = c_fin - lectura_previa;
        lectura_previa = c_fin;

        if (!contador_pausado) {
            total_eventos += pulsos_ciclo;
        }

        uint64_t delta_t_us = t_fin - t_inicio;
        double frec_hw = (double)pulsos_ventana / ((double)delta_t_us / 1000000.0);

        // --------------------------------------------------------------------
        // FASE 3: Selección de trama de telemetría según umbral de 10 kHz
        // --------------------------------------------------------------------
        if (frec_hw >= 10000.0) {
            // ALTA FRECUENCIA (>= 10 kHz): Cálculo directo por hardware
            // A 240 MHz transcurren 240 ciclos de reloj por cada microsegundo
            double ciclos_totales = (double)delta_t_us * 240.0;
            double duty_hw = ((double)ciclos_alto / ciclos_totales) * 100.0;
            if (duty_hw > 100.0) duty_hw = 100.0;

            // Formato Trama H: H,<pulsos_ventana>,<delta_t_us>,<total_eventos>,<duty_percent>
            printf("H,%lu,%llu,%llu,%.2f\n",
                   (unsigned long)pulsos_ventana,
                   (unsigned long long)delta_t_us,
                   (unsigned long long)total_eventos,
                   duty_hw);
        } else {
            // BAJA FRECUENCIA (< 10 kHz): Medición recíproca de ciclo completo
            // Timeout de 25 s (25.000.000 us) para permitir señales lentas de hasta 0,05 Hz
            uint64_t t_alto = 0, t_bajo = 0;
            if (medir_tiempos_us(PIN_MED, 25000000, &t_alto, &t_bajo) && (t_alto + t_bajo) > 0) {
                uint32_t c_post = leer_contador_continuo();
                if (!contador_pausado) {
                    total_eventos += (c_post - lectura_previa);
                }
                lectura_previa = c_post;

                // Formato Trama T: T,<t_alto_us>,<t_bajo_us>,<total_eventos>
                printf("T,%llu,%llu,%llu\n",
                       (unsigned long long)t_alto,
                       (unsigned long long)t_bajo,
                       (unsigned long long)total_eventos);
            } else {
                // SIN SEÑAL (Timeout expirado)
                uint32_t c_post = leer_contador_continuo();
                if (!contador_pausado) {
                    total_eventos += (c_post - lectura_previa);
                }
                lectura_previa = c_post;

                // Formato Trama Z: Z,0,0,<total_eventos>
                printf("Z,0,0,%llu\n", (unsigned long long)total_eventos);
            }
        }

        sleep_ms(250); // Intervalo de refresco hacia la interfaz de PC
    }
}

// ============================================================================
// CORE 0: INICIALIZACIÓN DEL SISTEMA Y RECEPCIÓN DE COMANDOS USB-CDC
// ============================================================================

/**
 * @brief Punto de entrada principal (Core 0).
 * Configura el reloj del sistema a 240 MHz, arranca el generador por defecto a 1 kHz,
 * lanza el Core 1 y queda a la escucha de comandos ASCII terminados en '\n' o '\r'.
 */
int main() {
    // Ajuste de voltaje interno (1,20 V) y frecuencia de reloj a 240 MHz
    vreg_set_voltage(VREG_VOLTAGE_1_20);
    sleep_ms(10);
    set_sys_clock_khz(240000, true);

    // Inicialización del LED integrado y parpadeo de arranque
    gpio_init(LED_PIN);
    gpio_set_dir(LED_PIN, GPIO_OUT);

    stdio_init_all();
    for (int i = 0; i < 3; i++) {
        gpio_put(LED_PIN, 1); sleep_ms(150);
        gpio_put(LED_PIN, 0); sleep_ms(150);
    }

    // Configuración inicial del generador: 1 kHz con 50 % de ciclo de trabajo
    gpio_set_function(PIN_GEN, GPIO_FUNC_PWM);
    set_pwm_output(PIN_GEN, 1000.0, 50.0);

    // Arranque del núcleo secundario encargado de la medición continua
    multicore_launch_core1(core1_entry);

    char buffer[64];
    uint idx = 0;

    // Bucle principal de lectura no bloqueante de comandos serie desde el PC
    while (true) {
        int c = getchar_timeout_us(0);

        if (c != PICO_ERROR_TIMEOUT) {
            if (c == '\r' || c == '\n') {
                buffer[idx] = '\0';

                if (idx > 0) {
                    // Comando 'R': Reinicia el contador acumulado de eventos
                    if (buffer[0] == 'R' && buffer[1] == '\0') {
                        reset_eventos_req = true;

                        gpio_put(LED_PIN, 1);
                        sleep_ms(30);
                        gpio_put(LED_PIN, 0);
                    } 
                    // Comando 'P': Alterna pausa/reanudación del contador de eventos
                    else if (buffer[0] == 'P' && buffer[1] == '\0') {
                        contador_pausado = !contador_pausado;

                        gpio_put(LED_PIN, 1);
                        sleep_ms(30);
                        gpio_put(LED_PIN, 0);
                    }
                    // Comando 'S,<frecuencia>,<duty>': Configura salida PWM del generador
                    else {
                        double nueva_frec = 0.0;
                        double nuevo_duty = 50.0;

                        if (sscanf(buffer, "S,%lf,%lf", &nueva_frec, &nuevo_duty) == 2) {
                            if (nueva_frec == 0.0) {
                                set_pwm_output(PIN_GEN, 0.0, 0.0);
                            } 
                            else if (nueva_frec >= 0.01 && nueva_frec <= 120000000.0 && 
                                     nuevo_duty >= 0.0 && nuevo_duty <= 100.0) {
                                set_pwm_output(PIN_GEN, nueva_frec, nuevo_duty);
                        
                                // Destello breve de confirmación en el LED de la placa
                                gpio_put(LED_PIN, 1);
                                sleep_ms(30);
                                gpio_put(LED_PIN, 0);
                            }
                        }
                    }
                }
                idx = 0; // Reinicia índice de buffer para la siguiente trama
            } else if (idx < sizeof(buffer) - 1) {
                buffer[idx++] = (char)c;
            }
        }
        sleep_ms(5);
    }

    return 0;
}