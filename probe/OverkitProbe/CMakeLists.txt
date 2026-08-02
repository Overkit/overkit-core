# Sonde Overkit — mod UE4SS C++, lecture seule (P1).
# Se compile dans un workspace qui fournit la cible UE4SS via
# add_subdirectory(RE-UE4SS) — voir probe/README.md.
cmake_minimum_required(VERSION 3.22)

set(TARGET OverkitProbe)
project(${TARGET} LANGUAGES CXX)

add_library(${TARGET} SHARED dllmain.cpp)

target_compile_features(${TARGET} PRIVATE cxx_std_23)
target_link_libraries(${TARGET} PRIVATE UE4SS)

# UE4SS charge les mods C++ sous Mods/<Nom>/dlls/main.dll
set_target_properties(${TARGET} PROPERTIES OUTPUT_NAME "main")
