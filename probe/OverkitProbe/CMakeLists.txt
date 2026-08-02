# Sonde Overkit — mod UE4SS C++, lecture seule (P1).
# Se compile dans un workspace qui fournit la cible UE4SS via
# add_subdirectory(RE-UE4SS) — voir probe/README.md.
cmake_minimum_required(VERSION 3.22)

set(TARGET OverkitProbe)
project(${TARGET} LANGUAGES CXX)

add_library(${TARGET} SHARED dllmain.cpp WsServer.cpp Explorer.cpp PalboxCollector.cpp)

target_compile_features(${TARGET} PRIVATE cxx_std_23)
# ws2_32/bcrypt/crypt32 : WebSocket maison (transport local, ADR-0004)
target_link_libraries(${TARGET} PRIVATE UE4SS ws2_32 bcrypt crypt32)

# UE4SS charge les mods C++ sous Mods/<Nom>/dlls/main.dll
set_target_properties(${TARGET} PROPERTIES OUTPUT_NAME "main")
