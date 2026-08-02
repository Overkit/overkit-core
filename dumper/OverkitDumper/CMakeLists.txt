# Dumper Overkit — mod UE4SS utilitaire (§4.1), jamais livré aux joueurs.
# Énumère les DataTables du jeu par réflexion et écrit les JSON bruts qui
# alimentent le pipeline dataset. Se compile dans le même workspace que la
# Sonde (add_subdirectory via jonction).
cmake_minimum_required(VERSION 3.22)

set(TARGET OverkitDumper)
project(${TARGET} LANGUAGES CXX)

add_library(${TARGET} SHARED dllmain.cpp)

target_compile_features(${TARGET} PRIVATE cxx_std_23)
target_link_libraries(${TARGET} PRIVATE UE4SS)

set_target_properties(${TARGET} PROPERTIES OUTPUT_NAME "main")
