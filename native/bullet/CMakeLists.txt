cmake_minimum_required(VERSION 3.20)
project(ymu_bullet_native VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(YMU_BULLET3_ROOT "${CMAKE_CURRENT_LIST_DIR}/../bullet3" CACHE PATH "Bullet3 source checkout")

set(BUILD_UNIT_TESTS OFF CACHE BOOL "" FORCE)
set(BUILD_BULLET2_DEMOS OFF CACHE BOOL "" FORCE)
set(BUILD_EXTRAS OFF CACHE BOOL "" FORCE)
set(BUILD_CPU_DEMOS OFF CACHE BOOL "" FORCE)
set(BUILD_OPENGL3_DEMOS OFF CACHE BOOL "" FORCE)
set(BUILD_BULLET3 OFF CACHE BOOL "" FORCE)
set(BUILD_PYBULLET OFF CACHE BOOL "" FORCE)
set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
set(INSTALL_LIBS OFF CACHE BOOL "" FORCE)

if(NOT EXISTS "${YMU_BULLET3_ROOT}/CMakeLists.txt")
    message(FATAL_ERROR "Bullet3 source checkout not found at ${YMU_BULLET3_ROOT}. Run: git submodule update --init --recursive native/bullet3")
endif()

set(YMU_BULLET3_SOURCE_DIR "${YMU_BULLET3_ROOT}")
add_subdirectory("${YMU_BULLET3_SOURCE_DIR}" bullet3_build EXCLUDE_FROM_ALL)

add_library(yohawing_mmd_unity_bullet SHARED src/ymu_bullet_api.cpp)
target_compile_definitions(yohawing_mmd_unity_bullet PRIVATE YMU_BULLET_BUILDING_DLL)
target_include_directories(yohawing_mmd_unity_bullet PRIVATE src "${YMU_BULLET3_SOURCE_DIR}/src")
target_link_libraries(yohawing_mmd_unity_bullet PRIVATE BulletDynamics BulletCollision LinearMath)
if(MSVC)
    target_compile_options(yohawing_mmd_unity_bullet PRIVATE /EHsc)
endif()

set(PLUGINS_DIR "${CMAKE_SOURCE_DIR}/../../packages/com.yohawing.mmd-loader/Runtime/Plugins/x86_64")
set_target_properties(yohawing_mmd_unity_bullet PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY_DEBUG   "${PLUGINS_DIR}"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${PLUGINS_DIR}"
    RUNTIME_OUTPUT_DIRECTORY         "${PLUGINS_DIR}"
    LIBRARY_OUTPUT_DIRECTORY_DEBUG   "${PLUGINS_DIR}"
    LIBRARY_OUTPUT_DIRECTORY_RELEASE "${PLUGINS_DIR}"
    LIBRARY_OUTPUT_DIRECTORY         "${PLUGINS_DIR}"
    PREFIX ""
)
