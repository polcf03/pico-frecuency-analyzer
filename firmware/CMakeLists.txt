# Generated Cmake Pico project file

cmake_minimum_required(VERSION 3.13)

set(CMAKE_C_STANDARD 11)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

# Initialise pico_sdk from installed location
# (note this can come from environment, CMake cache etc)

# == DO NOT EDIT THE FOLLOWING LINES for the Raspberry Pi Pico VS Code Extension to work ==
if(WIN32)
    set(USERHOME $ENV{USERPROFILE})
else()
    set(USERHOME $ENV{HOME})
endif()
set(sdkVersion 2.3.1)
set(toolchainVersion 15_2_Rel1)
set(picotoolVersion 2.3.1)
set(picoVscode ${USERHOME}/.pico-sdk/cmake/pico-vscode.cmake)
if (EXISTS ${picoVscode})
    include(${picoVscode})
endif()
# ====================================================================================
set(PICO_BOARD pico CACHE STRING "Board type")

# Pull in Raspberry Pi Pico SDK (must be before project)
include(pico_sdk_import.cmake)

project(frecuencimetro C CXX ASM)

# Initialise the Raspberry Pi Pico SDK
pico_sdk_init()

# Add executable. Default name is the project name, version 0.1
add_executable(frecuencimetro frecuencimetro.c)

pico_set_program_name(frecuencimetro "frecuencimetro")
pico_set_program_version(frecuencimetro "0.1")

# Modify the below lines to enable/disable output over UART/USB
pico_enable_stdio_uart(frecuencimetro 0)
pico_enable_stdio_usb(frecuencimetro 1)

# Add the standard include files to the build
target_include_directories(frecuencimetro PRIVATE
        ${CMAKE_CURRENT_LIST_DIR}
)

# Enlace de todas las librerias necesarias del SDK
target_link_libraries(frecuencimetro
        pico_stdlib
        pico_multicore      # Necesario para ejecutar el medidor en el Core 1
        hardware_pwm        # Necesario para la generacion y medicion por PWM
        hardware_clocks     # Necesario para el overclock a 240 MHz
        hardware_vreg       # Necesario para subir el voltaje a 1.20V
        hardware_irq        # Necesario para las interrupciones de 32-bit del medidor
)

pico_add_extra_outputs(frecuencimetro)