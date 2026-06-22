#include "mmd_bullet_api.h"

#include <btBulletDynamicsCommon.h>
#include <BulletDynamics/ConstraintSolver/btGeneric6DofSpring2Constraint.h>

#include <algorithm>
#include <array>
#include <cmath>
#include <exception>
#include <initializer_list>
#include <memory>
#include <string>
#include <vector>

struct ymu_bullet_world_s {
    std::unique_ptr<btDefaultCollisionConfiguration> collision_configuration;
    std::unique_ptr<btCollisionDispatcher> dispatcher;
    std::unique_ptr<btBroadphaseInterface> broadphase;
    std::unique_ptr<btSequentialImpulseConstraintSolver> solver;
    std::unique_ptr<btDiscreteDynamicsWorld> world;
    std::vector<std::unique_ptr<btCollisionShape>> collision_shapes;
    std::vector<std::unique_ptr<btMotionState>> motion_states;
    std::vector<std::unique_ptr<btRigidBody>> rigid_bodies;
    std::vector<btTransform> initial_transforms;
    std::vector<int> shape_kinds;
    std::vector<int> collision_groups;
    std::vector<int> collision_masks;
    std::vector<std::unique_ptr<btTypedConstraint>> constraints;
    std::vector<int> joint_body_a_indices;
    std::vector<int> joint_body_b_indices;
    std::vector<std::array<float, 3>> joint_positions;
    std::vector<std::array<float, 3>> joint_rotations;
    std::vector<std::array<float, 3>> joint_linear_lower_limits;
    std::vector<std::array<float, 3>> joint_linear_upper_limits;
    std::vector<std::array<float, 3>> joint_angular_lower_limits;
    std::vector<std::array<float, 3>> joint_angular_upper_limits;
    std::vector<std::array<float, 3>> joint_linear_springs;
    std::vector<std::array<float, 3>> joint_angular_springs;
    std::vector<std::array<float, 3>> joint_frame_a_positions;
    std::vector<std::array<float, 4>> joint_frame_a_rotations;
    std::vector<std::array<float, 3>> joint_frame_b_positions;
    std::vector<std::array<float, 4>> joint_frame_b_rotations;

    ~ymu_bullet_world_s()
    {
        if (!world) {
            return;
        }

        for (const auto& constraint : constraints) {
            if (constraint) {
                world->removeConstraint(constraint.get());
            }
        }

        for (const auto& body : rigid_bodies) {
            if (body) {
                world->removeRigidBody(body.get());
            }
        }
    }
};

namespace {
thread_local std::string g_last_error;

void set_last_error(const char *message)
{
    g_last_error = message ? message : "";
}

void clear_last_error()
{
    g_last_error.clear();
}

bool is_finite(float value)
{
    return std::isfinite(value);
}

bool all_finite(std::initializer_list<float> values)
{
    for (float value : values) {
        if (!is_finite(value)) {
            return false;
        }
    }

    return true;
}

bool finite_xyz(const float* values)
{
    return values
        && std::isfinite(values[0])
        && std::isfinite(values[1])
        && std::isfinite(values[2]);
}

int shape_kind_code(const char* shape_type)
{
    if (!shape_type) {
        return -1;
    }

    const std::string shape(shape_type);
    if (shape == "sphere") {
        return 0;
    }

    if (shape == "box") {
        return 1;
    }

    if (shape == "capsule") {
        return 2;
    }

    return -1;
}

// MMD/PMX rigid body collision filtering maps directly onto Bullet's broadphase
// semantics: a set bit means "collides with that group". The PMX 16-bit collision
// field is already the collide-with mask, so it must be passed through unchanged -
// inverting it (the previous behavior) turned a "collides with all" body (0xffff)
// into "collides with nothing" (0), which let hair/skirt bodies pass through the
// body colliders. This matches saba MMDPhysics.cpp:48-49 (filter), :152-155
// (addRigidBody(body, 1<<group, groupMask)) and :615-616 (raw m_collisionGroup).
//
// Return int (not signed short): group 15 (1<<15 == 0x8000) and high mask bits
// would otherwise sign-extend when promoted to the int parameters of addRigidBody.
int collision_group_bit(int collision_group)
{
    return 1 << std::clamp(collision_group, 0, 15);
}

int collision_mask_bits(int collision_mask)
{
    return std::clamp(collision_mask, 0, 0xffff) & 0xffff;
}

btVector3 to_vector3(const float* values)
{
    return btVector3(values[0], values[1], values[2]);
}

std::array<float, 3> to_array3(const float* values)
{
    return {values[0], values[1], values[2]};
}

void copy_array3(const std::array<float, 3>& source, float* destination)
{
    destination[0] = source[0];
    destination[1] = source[1];
    destination[2] = source[2];
}

std::array<float, 4> to_quaternion_array4(const btTransform& transform)
{
    btQuaternion rotation = transform.getRotation();
    return {rotation.x(), rotation.y(), rotation.z(), rotation.w()};
}

std::array<float, 3> to_origin_array3(const btTransform& transform)
{
    btVector3 origin = transform.getOrigin();
    return {origin.x(), origin.y(), origin.z()};
}

void copy_array4(const std::array<float, 4>& source, float* destination)
{
    destination[0] = source[0];
    destination[1] = source[1];
    destination[2] = source[2];
    destination[3] = source[3];
}

btQuaternion euler_zyx_to_quaternion(const float* rotation_xyz)
{
    btQuaternion rotation;
    rotation.setEulerZYX(rotation_xyz[2], rotation_xyz[1], rotation_xyz[0]);
    return rotation;
}

btTransform make_transform(const float* position_xyz, const float* rotation_xyz)
{
    btTransform transform;
    transform.setIdentity();
    transform.setOrigin(to_vector3(position_xyz));
    transform.setRotation(euler_zyx_to_quaternion(rotation_xyz));
    return transform;
}

bool validate_joint_body_indices(ymu_bullet_world_t* world, int body_a_index, int body_b_index)
{
    if (!world || !world->world) {
        set_last_error("world is null");
        return false;
    }

    if (body_a_index < 0 || body_b_index < 0
        || body_a_index >= static_cast<int>(world->rigid_bodies.size())
        || body_b_index >= static_cast<int>(world->rigid_bodies.size())) {
        set_last_error("joint body index is out of range");
        return false;
    }

    return true;
}

void configure_spring(btGeneric6DofSpring2Constraint& constraint, int dof_index, float stiffness)
{
    if (stiffness <= 0.0f) {
        return;
    }

    constraint.enableSpring(dof_index, true);
    constraint.setStiffness(dof_index, stiffness);
    constraint.setDamping(dof_index, 0.5f);
}

std::unique_ptr<btCollisionShape> create_shape(const char* shape_type, float size_x, float size_y, float size_z)
{
    const int shape_kind = shape_kind_code(shape_type);
    if (shape_kind == 0) {
        return std::make_unique<btSphereShape>(std::max(size_x, 0.0001f));
    }

    if (shape_kind == 1) {
        return std::make_unique<btBoxShape>(btVector3(
            std::max(size_x, 0.0001f),
            std::max(size_y, 0.0001f),
            std::max(size_z, 0.0001f)));
    }

    if (shape_kind == 2) {
        return std::make_unique<btCapsuleShape>(
            std::max(size_x, 0.0001f),
            std::max(size_y, 0.0001f));
    }

    return nullptr;
}
}

extern "C" {

YMU_BULLET_API const char* ymu_bullet_get_version(void)
{
    return "bullet-3.27";
}

YMU_BULLET_API const char* ymu_bullet_get_last_error(void)
{
    return g_last_error.c_str();
}

YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_create(ymu_bullet_world_t** out_world)
{
    if (!out_world) {
        set_last_error("out_world is null");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    try {
        clear_last_error();
        auto created = std::make_unique<ymu_bullet_world_t>();
        created->collision_configuration = std::make_unique<btDefaultCollisionConfiguration>();
        created->dispatcher = std::make_unique<btCollisionDispatcher>(created->collision_configuration.get());
        created->broadphase = std::make_unique<btDbvtBroadphase>();
        created->solver = std::make_unique<btSequentialImpulseConstraintSolver>();
        created->world = std::make_unique<btDiscreteDynamicsWorld>(
            created->dispatcher.get(),
            created->broadphase.get(),
            created->solver.get(),
            created->collision_configuration.get());
        created->world->setGravity(btVector3(0.0f, -98.0f, 0.0f));
        *out_world = created.release();
        return YMU_BULLET_STATUS_SUCCESS;
    }
    catch (const std::exception& ex) {
        set_last_error(ex.what());
        *out_world = nullptr;
        return YMU_BULLET_STATUS_NATIVE_ERROR;
    }
}

YMU_BULLET_API void ymu_bullet_world_destroy(ymu_bullet_world_t* world)
{
    delete world;
}

YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_reset(ymu_bullet_world_t* world)
{
    if (!world || !world->world) {
        set_last_error("world is null");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    clear_last_error();
    world->world->clearForces();

    // saba MMDRigidBody::Reset cleans each body's broadphase pairs so a re-seed after a frame jump
    // (scrub/seek) does not carry stale, deeply-penetrating contact manifolds into the next step
    // (which the solver would resolve violently, making secondary bodies explode).
    btOverlappingPairCache* pair_cache = world->world->getPairCache();
    btDispatcher* dispatcher = world->world->getDispatcher();

    const size_t body_count = world->rigid_bodies.size();
    for (size_t i = 0; i < body_count; i++) {
        btRigidBody* body = world->rigid_bodies[i].get();
        if (!body) {
            continue;
        }

        const btTransform& transform = world->initial_transforms[i];
        body->setWorldTransform(transform);
        // Also reset the interpolation transform: otherwise a kinematic (bone-following) body whose
        // interpolation transform is stale would have a huge velocity computed from the delta on the
        // first step after a reset, yanking the jointed dynamic chain apart.
        body->setInterpolationWorldTransform(transform);
        if (body->getMotionState()) {
            body->getMotionState()->setWorldTransform(transform);
        }

        body->setLinearVelocity(btVector3(0.0f, 0.0f, 0.0f));
        body->setAngularVelocity(btVector3(0.0f, 0.0f, 0.0f));
        body->setInterpolationLinearVelocity(btVector3(0.0f, 0.0f, 0.0f));
        body->setInterpolationAngularVelocity(btVector3(0.0f, 0.0f, 0.0f));
        body->clearForces();
        body->activate(true);

        if (pair_cache && dispatcher && body->getBroadphaseHandle()) {
            pair_cache->cleanProxyFromPairs(body->getBroadphaseHandle(), dispatcher);
        }
    }

    return YMU_BULLET_STATUS_SUCCESS;
}

// Seed-scoped settle: after the managed layer has teleported every body to its CURRENT bone-derived
// pose (via ymu_bullet_world_set_rigidbody_transform), this re-aligns each body's INTERPOLATION world
// transform with its (already current) world transform and zeroes all velocities + forces. It does NOT
// change the world transform.
//
// Why this is needed and why it is SEED-SCOPED only: ymu_bullet_world_set_rigidbody_transform sets the
// world + motion-state transform but leaves the interpolation world transform at whatever the previous
// ymu_bullet_world_reset wrote (the ORIGIN-bind initial_transforms). On the first stepSimulation,
// Bullet's saveKinematicState computes a kinematic (mass-0, bone-following) body's velocity from
// (currentWorld - interpolationWorld)/dt = (current pose - origin bind)/dt, a huge spurious velocity that
// is imparted through the 6-DoF joints into the pure-dynamic (mode-1) secondary bodies and explodes the
// chain. Re-syncing interpolation here makes that delta zero, so the first forward step computes no
// spurious kinematic velocity. This mirrors saba PMXModel::ResetPhysics, where bodies are reset to the
// CURRENT node global transform and end at rest with a consistent transform before stepping resumes.
//
// This must NOT be folded into the per-frame ymu_bullet_world_set_rigidbody_transform: the per-frame
// forward re-pin of kinematic bodies RELIES on the (currentWorld - previousInterpolationWorld)/dt delta
// to drag the jointed dynamic bodies along with the animated skeleton. Zeroing/syncing interpolation
// every frame would kill that drag (the "collider stops working / tunneling" regression).
YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_settle_to_current(ymu_bullet_world_t* world)
{
    if (!world || !world->world) {
        set_last_error("world is null");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    clear_last_error();
    world->world->clearForces();

    btOverlappingPairCache* pair_cache = world->world->getPairCache();
    btDispatcher* dispatcher = world->world->getDispatcher();

    const size_t body_count = world->rigid_bodies.size();
    for (size_t i = 0; i < body_count; i++) {
        btRigidBody* body = world->rigid_bodies[i].get();
        if (!body) {
            continue;
        }

        // Bodies are ALREADY at the current pose (managed teleport); only realign interpolation with it.
        body->setInterpolationWorldTransform(body->getWorldTransform());
        body->setLinearVelocity(btVector3(0.0f, 0.0f, 0.0f));
        body->setAngularVelocity(btVector3(0.0f, 0.0f, 0.0f));
        body->setInterpolationLinearVelocity(btVector3(0.0f, 0.0f, 0.0f));
        body->setInterpolationAngularVelocity(btVector3(0.0f, 0.0f, 0.0f));
        body->clearForces();
        body->activate(true);

        if (pair_cache && dispatcher && body->getBroadphaseHandle()) {
            pair_cache->cleanProxyFromPairs(body->getBroadphaseHandle(), dispatcher);
        }
    }

    return YMU_BULLET_STATUS_SUCCESS;
}

YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_step(ymu_bullet_world_t* world, float delta_time, int max_sub_steps)
{
    if (!world || !world->world) {
        set_last_error("world is null");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (!std::isfinite(delta_time) || delta_time < 0.0f) {
        set_last_error("delta_time must be non-negative finite");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (max_sub_steps < 0) {
        set_last_error("max_sub_steps must be non-negative");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    clear_last_error();
    world->world->stepSimulation(delta_time, max_sub_steps, 1.0f / 120.0f);
    return YMU_BULLET_STATUS_SUCCESS;
}

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
    int* out_native_index)
{
    if (!world || !world->world) {
        set_last_error("world is null");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (!out_native_index) {
        set_last_error("out_native_index is null");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (!all_finite({size_x, size_y, size_z, position_x, position_y, position_z, rotation_x, rotation_y, rotation_z, mass, linear_damping, angular_damping, friction, restitution})) {
        set_last_error("rigidbody values must be finite");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (collision_group < 0 || collision_group > 15) {
        set_last_error("collision_group must be between 0 and 15");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (collision_mask < 0 || collision_mask > 0xffff) {
        set_last_error("collision_mask must be between 0 and 65535");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (mass < 0.0f) {
        set_last_error("mass must be non-negative");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    try {
        const int shape_kind = shape_kind_code(shape_type);
        auto shape = create_shape(shape_type, size_x, size_y, size_z);
        if (!shape) {
            set_last_error("unsupported shape_type");
            return YMU_BULLET_STATUS_INVALID_ARGUMENT;
        }

        btTransform transform;
        transform.setIdentity();
        btQuaternion rotation;
        rotation.setEulerZYX(rotation_z, rotation_y, rotation_x);
        transform.setRotation(rotation);
        transform.setOrigin(btVector3(position_x, position_y, position_z));

        btVector3 inertia(0.0f, 0.0f, 0.0f);
        if (mass > 0.0f) {
            shape->calculateLocalInertia(mass, inertia);
        }

        auto motion_state = std::make_unique<btDefaultMotionState>(transform);
        btRigidBody::btRigidBodyConstructionInfo info(mass, motion_state.get(), shape.get(), inertia);
        info.m_linearDamping = linear_damping;
        info.m_angularDamping = angular_damping;
        info.m_friction = friction;
        info.m_restitution = restitution;
        auto body = std::make_unique<btRigidBody>(info);

        // Match saba (MMDPhysics.cpp:490-493,608-611): MMD physics bodies never
        // deactivate, and bone-following (mass 0) bodies are kinematic. A kinematic
        // body is moved by setWorldTransform each frame and Bullet derives its
        // velocity from the transform delta, so it imparts proper collision response
        // (push/drag) to dynamic bodies. A plain static (non-kinematic) mass-0 body
        // collides but cannot push or drag, and dynamic bodies resting against it
        // sleep and stop responding, which reads as "the collider stops working".
        body->setActivationState(DISABLE_DEACTIVATION);
        if (mass == 0.0f) {
            body->setCollisionFlags(body->getCollisionFlags() | btCollisionObject::CF_KINEMATIC_OBJECT);
        }

        int native_index = static_cast<int>(world->rigid_bodies.size());
        world->world->addRigidBody(body.get(), collision_group_bit(collision_group), collision_mask_bits(collision_mask));
        world->collision_shapes.emplace_back(std::move(shape));
        world->motion_states.emplace_back(std::move(motion_state));
        world->initial_transforms.emplace_back(transform);
        world->shape_kinds.emplace_back(shape_kind);
        world->collision_groups.emplace_back(collision_group);
        world->collision_masks.emplace_back(collision_mask);
        world->rigid_bodies.emplace_back(std::move(body));
        *out_native_index = native_index;
        clear_last_error();
        return YMU_BULLET_STATUS_SUCCESS;
    }
    catch (const std::exception& ex) {
        set_last_error(ex.what());
        return YMU_BULLET_STATUS_NATIVE_ERROR;
    }
}

YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_add_point2point_joint(
    ymu_bullet_world_t* world,
    int body_a_index,
    int body_b_index,
    float pivot_x,
    float pivot_y,
    float pivot_z,
    int* out_native_index)
{
    if (!world || !world->world) {
        set_last_error("world is null");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (!out_native_index) {
        set_last_error("out_native_index is null");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (body_a_index < 0 || body_b_index < 0
        || body_a_index >= static_cast<int>(world->rigid_bodies.size())
        || body_b_index >= static_cast<int>(world->rigid_bodies.size())) {
        set_last_error("joint body index is out of range");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (!all_finite({pivot_x, pivot_y, pivot_z})) {
        set_last_error("joint pivot must be finite");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    try {
        btVector3 pivot(pivot_x, pivot_y, pivot_z);
        btTransform transform_a = world->initial_transforms[body_a_index];
        btTransform transform_b = world->initial_transforms[body_b_index];
        btVector3 local_pivot_a = transform_a.inverse() * pivot;
        btVector3 local_pivot_b = transform_b.inverse() * pivot;
        auto constraint = std::make_unique<btPoint2PointConstraint>(
            *world->rigid_bodies[body_a_index],
            *world->rigid_bodies[body_b_index],
            local_pivot_a,
            local_pivot_b);
        int native_index = static_cast<int>(world->constraints.size());
        world->world->addConstraint(constraint.get(), true);
        world->constraints.emplace_back(std::move(constraint));
        world->joint_body_a_indices.emplace_back(body_a_index);
        world->joint_body_b_indices.emplace_back(body_b_index);
        world->joint_positions.emplace_back(std::array<float, 3>{pivot_x, pivot_y, pivot_z});
        world->joint_rotations.emplace_back(std::array<float, 3>{0.0f, 0.0f, 0.0f});
        world->joint_linear_lower_limits.emplace_back(std::array<float, 3>{0.0f, 0.0f, 0.0f});
        world->joint_linear_upper_limits.emplace_back(std::array<float, 3>{0.0f, 0.0f, 0.0f});
        world->joint_angular_lower_limits.emplace_back(std::array<float, 3>{0.0f, 0.0f, 0.0f});
        world->joint_angular_upper_limits.emplace_back(std::array<float, 3>{0.0f, 0.0f, 0.0f});
        world->joint_linear_springs.emplace_back(std::array<float, 3>{0.0f, 0.0f, 0.0f});
        world->joint_angular_springs.emplace_back(std::array<float, 3>{0.0f, 0.0f, 0.0f});
        world->joint_frame_a_positions.emplace_back(to_origin_array3(transform_a.inverse() * btTransform(btQuaternion::getIdentity(), pivot)));
        world->joint_frame_a_rotations.emplace_back(std::array<float, 4>{0.0f, 0.0f, 0.0f, 1.0f});
        world->joint_frame_b_positions.emplace_back(to_origin_array3(transform_b.inverse() * btTransform(btQuaternion::getIdentity(), pivot)));
        world->joint_frame_b_rotations.emplace_back(std::array<float, 4>{0.0f, 0.0f, 0.0f, 1.0f});
        *out_native_index = native_index;
        clear_last_error();
        return YMU_BULLET_STATUS_SUCCESS;
    }
    catch (const std::exception& ex) {
        set_last_error(ex.what());
        return YMU_BULLET_STATUS_NATIVE_ERROR;
    }
}

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
    int* out_native_index)
{
    if (!validate_joint_body_indices(world, body_a_index, body_b_index)) {
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (!out_native_index) {
        set_last_error("out_native_index is null");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (!finite_xyz(position_xyz)
        || !finite_xyz(rotation_xyz)
        || !finite_xyz(linear_lower_xyz)
        || !finite_xyz(linear_upper_xyz)
        || !finite_xyz(angular_lower_xyz)
        || !finite_xyz(angular_upper_xyz)
        || !finite_xyz(linear_spring_xyz)
        || !finite_xyz(angular_spring_xyz)) {
        set_last_error("6dof joint vectors must be non-null finite xyz arrays");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    try {
        btTransform joint_world = make_transform(position_xyz, rotation_xyz);
        btTransform frame_a = world->initial_transforms[body_a_index].inverse() * joint_world;
        btTransform frame_b = world->initial_transforms[body_b_index].inverse() * joint_world;
        auto constraint = std::make_unique<btGeneric6DofSpring2Constraint>(
            *world->rigid_bodies[body_a_index],
            *world->rigid_bodies[body_b_index],
            frame_a,
            frame_b,
            RO_XYZ);

        constraint->setLinearLowerLimit(to_vector3(linear_lower_xyz));
        constraint->setLinearUpperLimit(to_vector3(linear_upper_xyz));
        constraint->setAngularLowerLimit(to_vector3(angular_lower_xyz));
        constraint->setAngularUpperLimit(to_vector3(angular_upper_xyz));
        for (int i = 0; i < 3; i++) {
            configure_spring(*constraint, i, linear_spring_xyz[i]);
            configure_spring(*constraint, i + 3, angular_spring_xyz[i]);
        }

        constraint->setEquilibriumPoint();
        int native_index = static_cast<int>(world->constraints.size());
        world->world->addConstraint(constraint.get(), true);
        world->constraints.emplace_back(std::move(constraint));
        world->joint_body_a_indices.emplace_back(body_a_index);
        world->joint_body_b_indices.emplace_back(body_b_index);
        world->joint_positions.emplace_back(to_array3(position_xyz));
        world->joint_rotations.emplace_back(to_array3(rotation_xyz));
        world->joint_linear_lower_limits.emplace_back(to_array3(linear_lower_xyz));
        world->joint_linear_upper_limits.emplace_back(to_array3(linear_upper_xyz));
        world->joint_angular_lower_limits.emplace_back(to_array3(angular_lower_xyz));
        world->joint_angular_upper_limits.emplace_back(to_array3(angular_upper_xyz));
        world->joint_linear_springs.emplace_back(to_array3(linear_spring_xyz));
        world->joint_angular_springs.emplace_back(to_array3(angular_spring_xyz));
        world->joint_frame_a_positions.emplace_back(to_origin_array3(frame_a));
        world->joint_frame_a_rotations.emplace_back(to_quaternion_array4(frame_a));
        world->joint_frame_b_positions.emplace_back(to_origin_array3(frame_b));
        world->joint_frame_b_rotations.emplace_back(to_quaternion_array4(frame_b));
        *out_native_index = native_index;
        clear_last_error();
        return YMU_BULLET_STATUS_SUCCESS;
    }
    catch (const std::exception& ex) {
        set_last_error(ex.what());
        return YMU_BULLET_STATUS_NATIVE_ERROR;
    }
}

YMU_BULLET_API int ymu_bullet_world_get_rigidbody_count(ymu_bullet_world_t* world)
{
    if (!world) {
        return -1;
    }

    return static_cast<int>(world->rigid_bodies.size());
}

YMU_BULLET_API int ymu_bullet_world_get_joint_count(ymu_bullet_world_t* world)
{
    if (!world) {
        return -1;
    }

    return static_cast<int>(world->constraints.size());
}

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
    float* out_frame_b_rotation_xyzw)
{
    if (!world) {
        set_last_error("world is null");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (joint_index < 0 || joint_index >= static_cast<int>(world->constraints.size())) {
        set_last_error("joint index is out of range");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (!out_body_a_index || !out_body_b_index
        || !out_position_xyz || !out_rotation_xyz
        || !out_linear_lower_xyz || !out_linear_upper_xyz
        || !out_angular_lower_xyz || !out_angular_upper_xyz
        || !out_linear_spring_xyz || !out_angular_spring_xyz
        || !out_frame_a_position_xyz || !out_frame_a_rotation_xyzw
        || !out_frame_b_position_xyz || !out_frame_b_rotation_xyzw) {
        set_last_error("joint descriptor output pointer is null");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    *out_body_a_index = world->joint_body_a_indices[joint_index];
    *out_body_b_index = world->joint_body_b_indices[joint_index];
    copy_array3(world->joint_positions[joint_index], out_position_xyz);
    copy_array3(world->joint_rotations[joint_index], out_rotation_xyz);
    copy_array3(world->joint_linear_lower_limits[joint_index], out_linear_lower_xyz);
    copy_array3(world->joint_linear_upper_limits[joint_index], out_linear_upper_xyz);
    copy_array3(world->joint_angular_lower_limits[joint_index], out_angular_lower_xyz);
    copy_array3(world->joint_angular_upper_limits[joint_index], out_angular_upper_xyz);
    copy_array3(world->joint_linear_springs[joint_index], out_linear_spring_xyz);
    copy_array3(world->joint_angular_springs[joint_index], out_angular_spring_xyz);
    copy_array3(world->joint_frame_a_positions[joint_index], out_frame_a_position_xyz);
    copy_array4(world->joint_frame_a_rotations[joint_index], out_frame_a_rotation_xyzw);
    copy_array3(world->joint_frame_b_positions[joint_index], out_frame_b_position_xyz);
    copy_array4(world->joint_frame_b_rotations[joint_index], out_frame_b_rotation_xyzw);
    clear_last_error();
    return YMU_BULLET_STATUS_SUCCESS;
}

YMU_BULLET_API int ymu_bullet_world_get_rigidbody_shape_kind(ymu_bullet_world_t* world, int body_index)
{
    if (!world || body_index < 0 || body_index >= static_cast<int>(world->shape_kinds.size())) {
        return -1;
    }

    return world->shape_kinds[body_index];
}

YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_get_rigidbody_collision_filter(
    ymu_bullet_world_t* world,
    int body_index,
    int* out_collision_group,
    int* out_collision_mask)
{
    if (!world) {
        set_last_error("world is null");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (!out_collision_group || !out_collision_mask) {
        set_last_error("output collision filter buffers are null");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (body_index < 0 || body_index >= static_cast<int>(world->rigid_bodies.size())) {
        set_last_error("body_index is out of range");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    *out_collision_group = world->collision_groups[body_index];
    *out_collision_mask = world->collision_masks[body_index];
    clear_last_error();
    return YMU_BULLET_STATUS_SUCCESS;
}

YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_get_rigidbody_transform(
    ymu_bullet_world_t* world,
    int body_index,
    float* out_position_xyz,
    float* out_rotation_xyzw)
{
    if (!world || !world->world) {
        set_last_error("world is null");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (!out_position_xyz || !out_rotation_xyzw) {
        set_last_error("output transform buffers are null");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (body_index < 0 || body_index >= static_cast<int>(world->rigid_bodies.size())) {
        set_last_error("body_index is out of range");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    btTransform transform;
    btRigidBody* body = world->rigid_bodies[body_index].get();
    if (body->getMotionState()) {
        body->getMotionState()->getWorldTransform(transform);
    }
    else {
        transform = body->getWorldTransform();
    }

    const btVector3 origin = transform.getOrigin();
    const btQuaternion rotation = transform.getRotation();
    out_position_xyz[0] = origin.x();
    out_position_xyz[1] = origin.y();
    out_position_xyz[2] = origin.z();
    out_rotation_xyzw[0] = rotation.x();
    out_rotation_xyzw[1] = rotation.y();
    out_rotation_xyzw[2] = rotation.z();
    out_rotation_xyzw[3] = rotation.w();
    clear_last_error();
    return YMU_BULLET_STATUS_SUCCESS;
}

YMU_BULLET_API ymu_bullet_status_t ymu_bullet_world_set_rigidbody_transform(
    ymu_bullet_world_t* world,
    int body_index,
    const float* position_xyz,
    const float* rotation_xyzw)
{
    if (!world || !world->world) {
        set_last_error("world is null");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (body_index < 0 || body_index >= static_cast<int>(world->rigid_bodies.size())) {
        set_last_error("body_index is out of range");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (!position_xyz || !rotation_xyzw) {
        set_last_error("input transform buffers are null");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    if (!all_finite({
        position_xyz[0], position_xyz[1], position_xyz[2],
        rotation_xyzw[0], rotation_xyzw[1], rotation_xyzw[2], rotation_xyzw[3]})) {
        set_last_error("rigidbody transform values must be finite");
        return YMU_BULLET_STATUS_INVALID_ARGUMENT;
    }

    btTransform transform;
    transform.setIdentity();
    transform.setOrigin(btVector3(position_xyz[0], position_xyz[1], position_xyz[2]));
    transform.setRotation(btQuaternion(rotation_xyzw[0], rotation_xyzw[1], rotation_xyzw[2], rotation_xyzw[3]).normalized());
    btRigidBody* body = world->rigid_bodies[body_index].get();
    body->setWorldTransform(transform);
    if (body->getMotionState()) {
        body->getMotionState()->setWorldTransform(transform);
    }

    body->setLinearVelocity(btVector3(0.0f, 0.0f, 0.0f));
    body->setAngularVelocity(btVector3(0.0f, 0.0f, 0.0f));
    body->clearForces();
    body->activate(true);
    clear_last_error();
    return YMU_BULLET_STATUS_SUCCESS;
}

}
