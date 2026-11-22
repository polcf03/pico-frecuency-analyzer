#include <stdio.h>          // printf, sscanf, getchar_timeout_us
#include <stdlib.h>         // atof
#include "pico/stdlib.h"    // stdio_init_all, sleep_ms, sleep_us
#include "pico/multicore.h" // multicore_launch_core1
#include "hardware/clocks.h"// set_sys_clock_khz
#include "hardware/vreg.h"  // vreg_set_voltage
#include "hardware/pwm.h"   // Funciones de PWM
#include "hardware/irq.h"   // Gestión de interrupciones
#include "hardware/sync.h"  // save_and_disable_interrupts, restore_interrupts

#define PIN_GEN 16 // Pin de salida del generador de señal
#define PIN_MED 15 // Pin de entrada del medidor de frecuencia
#define LED_PIN 25 // Pin del LED integrado
#define PIN_DUTY 13 // Puenteado físicamente con PIN_MED (Slice 6, Canal B)

static volatile uint32_t duty_overflow_count = 0;
static uint pwm_slice_duty = 0;
static volatile uint32_t pwm_overflow_count = 0; // Contador de desbordamientos del PWM
static volatile uint64_t total_eventos = 0; // Contador acumulado de eventos
static volatile bool reset_eventos_req = false;
static uint pwm_slice_med = 0;                   // Slice del PWM para el medidor de frecuencia
static volatile bool contador_pausado = false;

static struct repeating_timer timer_bf; // Timer para frecuencias bajas (< 15 Hz)
static bool timer_bf_activo = false; // Indica si el timer de baja frecuencia está activo
static uint64_t bf_t_alto_us = 500000; // Tiempo en alto (us) para frecuencias bajas (< 15 Hz)
static uint64_t bf_t_bajo_us = 500000; // Tiempo en bajo (us) para frecuencias bajas (< 15 Hz)
static bool bf_estado_pin = false; // Estado actual del pin de salida para frecuencias bajas (< 15 Hz)

// Callback de interrupción de timer para frecuencias bajas (< 15 Hz)
bool timer_frec_bajas(struct repeating_timer *t) {
    if (bf_t_alto_us == 0 || bf_t_bajo_us == 0) {
        return false; // Detener el timer si el duty es 0% o 100%
    }

    if (bf_estado_pin) {
        gpio_put(PIN_GEN, 0);
        bf_estado_pin = false;
        t->delay_us = -(int64_t)bf_t_bajo_us;
    } else {
        gpio_put(PIN_GEN, 1);
        bf_estado_pin = true;
        t->delay_us = -(int64_t)bf_t_alto_us;
    }
    return true;
}

void on_pwm_wrap(void) 
{
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

static uint32_t leer_contador_continuo(void) 
{
    uint32_t flags = save_and_disable_interrupts();
    uint32_t overflows = pwm_overflow_count;
    if (pwm_get_irq_status_mask() & (1u << pwm_slice_med)) {
        pwm_clear_irq(pwm_slice_med);
        pwm_overflow_count++;
        overflows = pwm_overflow_count;
    }
    uint16_t cuenta_raw = pwm_get_counter(pwm_slice_med);
    restore_interrupts(flags);

    return (overflows << 16) | cuenta_raw;
}
static uint32_t leer_contador_duty(void) 
{
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
// Mide por separado el tiempo en ALTO (t_alto) y en BAJO (t_bajo) en microsegundos
static bool medir_tiempos_us(uint pin, uint32_t timeout_us, uint64_t *t_alto, uint64_t *t_bajo) 
{
    uint64_t limite = time_us_64() + timeout_us;

    while (gpio_get(pin))  { if (time_us_64() > limite) return false; } // Esperar nivel bajo
    while (!gpio_get(pin)) { if (time_us_64() > limite) return false; } // Esperar flanco de subida
    uint64_t t1 = time_us_64();

    while (gpio_get(pin))  { if (time_us_64() > limite) return false; } // Esperar flanco de bajada
    uint64_t t2 = time_us_64();

    while (!gpio_get(pin)) { if (time_us_64() > limite) return false; } // Esperar siguiente flanco de subida
    uint64_t t3 = time_us_64();

    *t_alto = t2 - t1;
    *t_bajo = t3 - t2;
    return true;
}

// Configura frecuencia (Hz) y Duty Cycle (0.0 a 100.0 %) del generador PWM
double set_pwm_output(uint pin, double frec_target_hz, double duty_percent) {
    uint slice_num = pwm_gpio_to_slice_num(pin);
    uint chan = pwm_gpio_to_channel(pin);

    // Detener siempre el timer de baja frecuencia antes de tocar nada
    if (timer_bf_activo) {
        cancel_repeating_timer(&timer_bf);
        timer_bf_activo = false;
    }

    // Apagar salida si frecuencia <= 0
    if (frec_target_hz <= 0.0) {
        pwm_set_enabled(slice_num, false);
        gpio_set_function(pin, GPIO_FUNC_SIO);
        gpio_set_dir(pin, GPIO_OUT);
        gpio_put(pin, 0);
        return 0.0;
    }

    if (duty_percent < 0.0) duty_percent = 0.0;
    if (duty_percent > 100.0) duty_percent = 100.0;

    // MODO 1: BAJA FRECUENCIA (< 16 Hz -> Timer hardware de 64 bits)
    if (frec_target_hz < 16.0) {
        pwm_set_enabled(slice_num, false);
        gpio_set_function(pin, GPIO_FUNC_SIO);
        gpio_set_dir(pin, GPIO_OUT);

        double periodo_us = 1000000.0 / frec_target_hz;
        uint64_t alto = (uint64_t)(periodo_us * (duty_percent / 100.0) + 0.5);
        uint64_t bajo = (alto >= (uint64_t)periodo_us) ? 0 : ((uint64_t)periodo_us - alto);

        // Casos extremos (0% o 100%): nivel fijo SIN arrancar interrupciones
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

    // MODO 2: ALTA/MEDIA FRECUENCIA (>= 16 Hz -> PWM Hardware puro)
    gpio_set_function(pin, GPIO_FUNC_PWM);

    double f_sys = 240000000.0;
    float clkdiv = 1.0f;

    if (frec_target_hz < 4000.0) clkdiv = 250.0f;

    double wrap_exacto = (f_sys / (clkdiv * frec_target_hz)) - 1.0;
    uint32_t wrap = (uint32_t)(wrap_exacto + 0.5);

    if (wrap < 1) wrap = 1;
    if (wrap > 65535) wrap = 65535;

    uint32_t level = (uint32_t)(((double)(wrap + 1) * (duty_percent / 100.0)) + 0.5);
    if (level > 65535) level = 65535;

    double frec_real = f_sys / (clkdiv * (double)(wrap + 1));

    pwm_set_enabled(slice_num, false);
    pwm_set_clkdiv(slice_num, clkdiv);
    pwm_set_wrap(slice_num, (uint16_t)wrap);
    pwm_set_chan_level(slice_num, chan, (uint16_t)level);
    pwm_set_enabled(slice_num, true);

    return frec_real;
}

// CORE 1: MEDIDOR HARDWARE CONTINUO Y ENVÍO DE TRAMAS CSV (4 CAMPOS)
void core1_entry(void) {
    // --- Configuración existente de PIN_MED (Conteo de flancos) ---
    assert(pwm_gpio_to_channel(PIN_MED) == PWM_CHAN_B);
    pwm_slice_med = pwm_gpio_to_slice_num(PIN_MED);
    gpio_set_function(PIN_MED, GPIO_FUNC_PWM);

    pwm_config cfg = pwm_get_default_config();
    pwm_config_set_clkdiv_mode(&cfg, PWM_DIV_B_RISING);
    pwm_config_set_clkdiv(&cfg, 1.0f);
    pwm_config_set_wrap(&cfg, 0xFFFF);
    pwm_init(pwm_slice_med, &cfg, false);

    // --- NUEVA configuración de PIN_DUTY (Tiempo en nivel ALTO) ---
    assert(pwm_gpio_to_channel(PIN_DUTY) == PWM_CHAN_B);
    pwm_slice_duty = pwm_gpio_to_slice_num(PIN_DUTY);
    gpio_set_function(PIN_DUTY, GPIO_FUNC_PWM);

    pwm_config cfg_duty = pwm_get_default_config();
    pwm_config_set_clkdiv_mode(&cfg_duty, PWM_DIV_B_HIGH); // Cuenta ciclos a 240 MHz en HIGH
    pwm_config_set_clkdiv(&cfg_duty, 1.0f);
    pwm_config_set_wrap(&cfg_duty, 0xFFFF);
    pwm_init(pwm_slice_duty, &cfg_duty, false);

    // Activar interrupciones para ambos slices
    pwm_clear_irq(pwm_slice_med);
    pwm_clear_irq(pwm_slice_duty);
    pwm_set_irq_enabled(pwm_slice_med, true);
    pwm_set_irq_enabled(pwm_slice_duty, true);
    irq_set_exclusive_handler(PWM_IRQ_WRAP, on_pwm_wrap);
    irq_set_enabled(PWM_IRQ_WRAP, true);

    // Arrancar ambos contadores
    pwm_set_counter(pwm_slice_med, 0);
    pwm_set_counter(pwm_slice_duty, 0);
    pwm_overflow_count = 0;
    duty_overflow_count = 0;
    pwm_set_enabled(pwm_slice_med, true);
    pwm_set_enabled(pwm_slice_duty, true);

    uint32_t lectura_previa = 0;

    while (true) {
        if (reset_eventos_req) {
            lectura_previa = leer_contador_continuo();
            total_eventos = 0;
            reset_eventos_req = false;
        }

        // 1. Ventana de medición de 100 ms leyendo AMBOS contadores a la vez
        uint32_t c_inicio = leer_contador_continuo();
        uint32_t d_inicio = leer_contador_duty();
        uint64_t t_inicio = time_us_64();
        
        sleep_us(100000);
        
        uint32_t c_fin = leer_contador_continuo();
        uint32_t d_fin = leer_contador_duty();
        uint64_t t_fin = time_us_64();

        // 2. Cálculos de ventana
        uint32_t pulsos_ventana = c_fin - c_inicio;
        uint32_t ciclos_alto = d_fin - d_inicio;
        uint32_t pulsos_ciclo = c_fin - lectura_previa;
        lectura_previa = c_fin;
        if (!contador_pausado) {
            total_eventos += pulsos_ciclo;
        }

        uint64_t delta_t_us = t_fin - t_inicio;
        double frec_hw = (double)pulsos_ventana / ((double)delta_t_us / 1000000.0);

        // 3. Envío de trama CSV
        if (frec_hw >= 10000.0) {
            // Cada microsegundo son 240 ciclos de reloj (f_sys = 240 MHz)
            double ciclos_totales = (double)delta_t_us * 240.0;
            double duty_hw = ((double)ciclos_alto / ciclos_totales) * 100.0;
            if (duty_hw > 100.0) duty_hw = 100.0;

            // Enviamos trama 'H' (High-freq con Duty): H,<pulsos>,<delta_t_us>,<total_eventos>,<duty>
            printf("H,%lu,%llu,%llu,%.2f\n",
                   (unsigned long)pulsos_ventana,
                   (unsigned long long)delta_t_us,
                   (unsigned long long)total_eventos,
                   duty_hw);
        } else {
            // Timeout de 25 s (25,000,000 us) para capturar señales lentas hasta 0,05 Hz
            uint64_t t_alto = 0, t_bajo = 0;
            if (medir_tiempos_us(PIN_MED, 25000000, &t_alto, &t_bajo) && (t_alto + t_bajo) > 0) {
                uint32_t c_post = leer_contador_continuo();
                if (!contador_pausado) {
                    total_eventos += (c_post - lectura_previa);
                }
                lectura_previa = c_post;

                printf("T,%llu,%llu,%llu\n",
                       (unsigned long long)t_alto,
                       (unsigned long long)t_bajo,
                       (unsigned long long)total_eventos);
            } else {
                uint32_t c_post = leer_contador_continuo();
                if (!contador_pausado) {
                    total_eventos += (c_post - lectura_previa);
                }
                lectura_previa = c_post;

                printf("Z,0,0,%llu\n", (unsigned long long)total_eventos);
            }
        }

        sleep_ms(250);
    }
}
// CORE 0: GENERADOR PWM Y RECEPCIÓN DE COMANDOS DESDE C#
int main() {
    vreg_set_voltage(VREG_VOLTAGE_1_20);
    sleep_ms(10);
    set_sys_clock_khz(240000, true);

    gpio_init(LED_PIN);
    gpio_set_dir(LED_PIN, GPIO_OUT);

    stdio_init_all();
    for(int i = 0; i < 3; i++) {
        gpio_put(LED_PIN, 1); sleep_ms(150);
        gpio_put(LED_PIN, 0); sleep_ms(150);
    }

    gpio_set_function(PIN_GEN, GPIO_FUNC_PWM);
    set_pwm_output(PIN_GEN, 1000.0, 50.0); // Arranca por defecto a 1 kHz, 50% duty

    multicore_launch_core1(core1_entry);

    char buffer[64];
    uint idx = 0;

    while (true) {
        int c = getchar_timeout_us(0);

        if (c != PICO_ERROR_TIMEOUT) {
            if (c == '\r' || c == '\n') {
                buffer[idx] = '\0';

                if (idx > 0) {
                    if (buffer[0] == 'R' && buffer[1] == '\0') {
                        reset_eventos_req = true;

                        gpio_put(LED_PIN, 1);
                        sleep_ms(30);
                        gpio_put(LED_PIN, 0);
                    } 
                    else if (buffer[0] == 'P' && buffer[1] == '\0') {
                        contador_pausado = !contador_pausado;

                        gpio_put(LED_PIN, 1);
                        sleep_ms(30);
                        gpio_put(LED_PIN, 0);
                    }
                    else {
                        double nueva_frec = 0.0;
                        double nuevo_duty = 50.0;

                        // Parsea la trama enviada por C#: "S,<frecuencia>,<duty>"
                        if (sscanf(buffer, "S,%lf,%lf", &nueva_frec, &nuevo_duty) == 2) {
                            if (nueva_frec == 0.0) {
                                set_pwm_output(PIN_GEN, 0.0, 0.0);
                            } 
                            else if (nueva_frec >= 0.01 && nueva_frec <= 120000000.0 && nuevo_duty >= 0.0 && nuevo_duty <= 100.0) {
                                set_pwm_output(PIN_GEN, nueva_frec, nuevo_duty);
                        
                                gpio_put(LED_PIN, 1);
                                sleep_ms(30);
                                gpio_put(LED_PIN, 0);
                            }
                        }
                    }
                }
                idx = 0;
            } else if (idx < sizeof(buffer) - 1) {
                buffer[idx++] = (char)c;
            }
        }
        sleep_ms(5);
    }

    return 0;
}