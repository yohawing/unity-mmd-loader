#pragma once

#include <stddef.h>

#ifdef _WIN32
  #ifdef YMU_BULLET_BUILDING_DLL
    #define YMU_BULLET_API __declspec(dllexport)
  #else
    #define YMU_BULLET_API __declspec(dllimport)
  #endif
#else
  #define YMU_BULLET_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef enum ymu_bullet_status_t {
    YMU_BULLET_STATUS_SUCCESS = 0,
    YMU_BULLET_STATUS_INVALID_ARGUMENT = 1,
    YMU_BULLET_STATUS_NATIVE_ERROR = 2
} ymu_bullet_status_t;

typedef struct ymu_bullet_world_s ymu_bullet_world_t;

YMU_BULLET_API const char* ymu_bullet_get_version(void);
YMU_BULLET_API const char* ymu_bullet_get_last_error(void);
YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_create(ymu_bullet_world_t** out_world);
YMU_BULLET_API void ymu_bullet_world_destroy(ymu_bullet_world_t* world);
YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_reset(ymu_bullet_world_t* world);
// Seed-scoped: re-align each body's interpolation world transform with its (already current) world
// transform and zero all velocities/forces, without changing the world transform. Call AFTER teleporting
// bodies to the current pose and BEFORE the first forward step so the kinematic delta is not spurious.
YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_settle_to_current(ymu_bullet_world_t* world);
YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_step(ymu_bullet_world_t* world, float delta_time, int max_sub_steps);
YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_add_rigidbody(
    ymu_bullet_world_t* world,
    const char* shape_type,
    float size_x,
    float size_y,
    float size_z,
    float position_x,
    float position_y,
    float position_z,
    float rotation_x,
    float rotation_y,
    float rotation_z,
    float mass,
    float linear_damping,
    float angular_damping,
    float friction,
    float restitution,
    int collision_group,
    int collision_mask,
    int* out_native_index);
YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_add_point2point_joint(
    ymu_bullet_world_t* world,
    int body_a_index,
    int body_b_index,
    float pivot_x,
    float pivot_y,
    float pivot_z,
    int* out_native_index);
YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_add_6dof_spring_joint(
    ymu_bullet_world_t* world,
    int body_a_index,
    int body_b_index,
    const float* position_xyz,
    const float* rotation_xyz,
    const float* linear_lower_xyz,
    const float* linear_upper_xyz,
    const float* angular_lower_xyz,
    const float* angular_upper_xyz,
    const float* linear_spring_xyz,
    const float* angular_spring_xyz,
    int* out_native_index);
YMU_BULLET_API int ymu_bullet_world_get_rigidbody_count(ymu_bullet_world_t* world);
YMU_BULLET_API int ymu_bullet_world_get_joint_count(ymu_bullet_world_t* world);
YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_get_6dof_spring_joint_descriptor(
    ymu_bullet_world_t* world,
    int joint_index,
    int* out_body_a_index,
    int* out_body_b_index,
    float* out_position_xyz,
    float* out_rotation_xyz,
    float* out_linear_lower_xyz,
    float* out_linear_upper_xyz,
    float* out_angular_lower_xyz,
    float* out_angular_upper_xyz,
    float* out_linear_spring_xyz,
    float* out_angular_spring_xyz,
    float* out_frame_a_position_xyz,
    float* out_frame_a_rotation_xyzw,
    float* out_frame_b_position_xyz,
    float* out_frame_b_rotation_xyzw);
YMU_BULLET_API int ymu_bullet_world_get_rigidbody_shape_kind(ymu_bullet_world_t* world, int body_index);
YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_get_rigidbody_collision_filter(
    ymu_bullet_world_t* world,
    int body_index,
    int* out_collision_group,
    int* out_collision_mask);
YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_get_rigidbody_transform(
    ymu_bullet_world_t* world,
    int body_index,
    float* out_position_xyz,
    float* out_rotation_xyzw);
YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_set_rigidbody_transform(
    ymu_bullet_world_t* world,
    int body_index,
    const float* position_xyz,
    const float* rotation_xyzw);

#ifdef __cplusplus
}
#endif
