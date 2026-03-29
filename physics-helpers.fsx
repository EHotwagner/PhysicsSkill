/// PhysicsHelpers — Estimation, collision detection, and utility functions
/// for PhysicsSandbox F# scripting.
///
/// Usage:
///   #load "/home/developer/projects/PhysicsSkill/physics-helpers.fsx"
///   open PhysicsHelpers
module PhysicsHelpers

open System
open System.Numerics

// ============================================================================
// Vector3 convenience constructors
// ============================================================================

let v3 (x: float, y: float, z: float) = Vector3(float32 x, float32 y, float32 z)
let v3f (x: float32, y: float32, z: float32) = Vector3(x, y, z)
let toTuple (v: Vector3) = (float v.X, float v.Y, float v.Z)

let midpoint (a: Vector3) (b: Vector3) = (a + b) * 0.5f
let direction (a: Vector3) (b: Vector3) = Vector3.Normalize(b - a)
let centroid (pts: Vector3 list) =
    let sum = pts |> List.fold (+) Vector3.Zero
    sum / float32 pts.Length

// ============================================================================
// Physics estimation — forces, impulses, projectiles
// ============================================================================

let gravity = 9.81f

/// Impulse needed to achieve a target velocity: impulse = mass * velocity
let impulseForVelocity (mass: float32) (targetSpeed: float32) : float32 =
    mass * targetSpeed

/// Time for an object to fall from height h (starting from rest): t = sqrt(2h/g)
let fallTime (height: float32) : float32 =
    if height <= 0.0f then 0.0f
    else sqrt (2.0f * height / gravity)

/// Velocity after falling from height h: v = sqrt(2gh)
let fallVelocity (height: float32) : float32 =
    if height <= 0.0f then 0.0f
    else sqrt (2.0f * gravity * height)

/// Max height reached when launched upward at speed v: h = v^2 / (2g)
let maxHeight (upwardSpeed: float32) : float32 =
    upwardSpeed * upwardSpeed / (2.0f * gravity)

/// Kinetic energy: KE = 0.5 * m * v^2
let kineticEnergy (mass: float32) (speed: float32) : float32 =
    0.5f * mass * speed * speed

/// Momentum: p = m * v
let momentum (mass: float32) (speed: float32) : float32 =
    mass * speed

/// Projectile range on flat ground (no air resistance)
let projectileRange (launchSpeed: float32) (launchAngleDeg: float32) : float32 =
    let rad = launchAngleDeg * MathF.PI / 180.0f
    launchSpeed * launchSpeed * sin (2.0f * rad) / gravity

/// Projectile max height
let projectileMaxHeight (launchSpeed: float32) (launchAngleDeg: float32) : float32 =
    let rad = launchAngleDeg * MathF.PI / 180.0f
    let vy = launchSpeed * sin rad
    vy * vy / (2.0f * gravity)

/// Projectile flight time (total, flat ground)
let projectileFlightTime (launchSpeed: float32) (launchAngleDeg: float32) : float32 =
    let rad = launchAngleDeg * MathF.PI / 180.0f
    2.0f * launchSpeed * sin rad / gravity

/// Position of projectile at time t. Returns (horizontal, vertical) displacement.
let projectilePositionAt (launchSpeed: float32) (launchAngleDeg: float32) (t: float32) : float32 * float32 =
    let rad = launchAngleDeg * MathF.PI / 180.0f
    let vx = launchSpeed * cos rad
    let vy = launchSpeed * sin rad
    (vx * t, vy * t - 0.5f * gravity * t * t)

/// Impulse vector to launch from fromPos to toPos in given flight time (accounts for gravity)
let impulseToHit (mass: float32) (fromPos: Vector3) (toPos: Vector3) (flightTime: float32) : Vector3 =
    let d = toPos - fromPos
    let vx = d.X / flightTime
    let vy = (d.Y + 0.5f * gravity * flightTime * flightTime) / flightTime
    let vz = d.Z / flightTime
    Vector3(mass * vx, mass * vy, mass * vz)

/// Impulse for a direct straight-line throw (ignoring gravity arc)
let impulseDirectThrow (mass: float32) (fromPos: Vector3) (toPos: Vector3) (speed: float32) : Vector3 =
    let dir = direction fromPos toPos
    dir * (mass * speed)

// ============================================================================
// Collision primitives
// ============================================================================

/// Axis-Aligned Bounding Box
type AABB =
    { Center: Vector3
      HalfExtents: Vector3 }

    member this.Min = this.Center - this.HalfExtents
    member this.Max = this.Center + this.HalfExtents

/// Bounding Sphere
type BoundingSphere =
    { Center: Vector3
      Radius: float32 }

/// Oriented Bounding Box — center, half-extents in local space, and 3 orthonormal axes
type OBB =
    { Center: Vector3
      HalfExtents: Vector3
      AxisX: Vector3       // local X axis (unit vector in world space)
      AxisY: Vector3       // local Y axis
      AxisZ: Vector3 }     // local Z axis

    /// Create an OBB from center, half-extents, and a rotation quaternion
    static member FromQuaternion(center: Vector3, halfExtents: Vector3, rotation: Quaternion) =
        { Center = center
          HalfExtents = halfExtents
          AxisX = Vector3.Transform(Vector3.UnitX, rotation)
          AxisY = Vector3.Transform(Vector3.UnitY, rotation)
          AxisZ = Vector3.Transform(Vector3.UnitZ, rotation) }

    /// Create an OBB from center, half-extents, and Euler angles (degrees)
    static member FromEulerDeg(center: Vector3, halfExtents: Vector3, yawDeg: float32, pitchDeg: float32, rollDeg: float32) =
        let toRad d = d * MathF.PI / 180.0f
        let q = Quaternion.CreateFromYawPitchRoll(toRad yawDeg, toRad pitchDeg, toRad rollDeg)
        OBB.FromQuaternion(center, halfExtents, q)

    /// Create an axis-aligned OBB (equivalent to AABB)
    static member AxisAligned(center: Vector3, halfExtents: Vector3) =
        { Center = center; HalfExtents = halfExtents
          AxisX = Vector3.UnitX; AxisY = Vector3.UnitY; AxisZ = Vector3.UnitZ }

    /// The 3 local axes as a list
    member this.Axes = [this.AxisX; this.AxisY; this.AxisZ]

    /// Get the 8 corner vertices in world space
    member this.Corners =
        [| for sx in [-1.0f; 1.0f] do
             for sy in [-1.0f; 1.0f] do
               for sz in [-1.0f; 1.0f] do
                 this.Center
                 + this.AxisX * (sx * this.HalfExtents.X)
                 + this.AxisY * (sy * this.HalfExtents.Y)
                 + this.AxisZ * (sz * this.HalfExtents.Z) |]

// ============================================================================
// Sphere vs Sphere
// ============================================================================

let sphereVsSphere (a: BoundingSphere) (b: BoundingSphere) : bool =
    Vector3.Distance(a.Center, b.Center) < a.Radius + b.Radius

let sphereVsSpherePenetration (a: BoundingSphere) (b: BoundingSphere) : float32 =
    (a.Radius + b.Radius) - Vector3.Distance(a.Center, b.Center)

let sphereVsSphereContact (a: BoundingSphere) (b: BoundingSphere) =
    let d = Vector3.Distance(a.Center, b.Center)
    let pen = (a.Radius + b.Radius) - d
    if pen <= 0.0f then None
    else
        let normal = direction a.Center b.Center
        let contact = a.Center + normal * a.Radius
        Some (contact, normal, pen)

// ============================================================================
// AABB vs AABB
// ============================================================================

let aabbVsAabb (a: AABB) (b: AABB) : bool =
    let aMin = a.Min
    let aMax = a.Max
    let bMin = b.Min
    let bMax = b.Max
    aMin.X <= bMax.X && aMax.X >= bMin.X &&
    aMin.Y <= bMax.Y && aMax.Y >= bMin.Y &&
    aMin.Z <= bMax.Z && aMax.Z >= bMin.Z

let aabbVsAabbPenetration (a: AABB) (b: AABB) =
    let dx = (a.HalfExtents.X + b.HalfExtents.X) - abs (a.Center.X - b.Center.X)
    let dy = (a.HalfExtents.Y + b.HalfExtents.Y) - abs (a.Center.Y - b.Center.Y)
    let dz = (a.HalfExtents.Z + b.HalfExtents.Z) - abs (a.Center.Z - b.Center.Z)
    if dx <= 0.0f || dy <= 0.0f || dz <= 0.0f then None
    else Some (Vector3(dx, dy, dz))

// ============================================================================
// Sphere vs AABB
// ============================================================================

let closestPointOnAabb (box: AABB) (point: Vector3) : Vector3 =
    Vector3.Clamp(point, box.Min, box.Max)

let sphereVsAabb (sphere: BoundingSphere) (box: AABB) : bool =
    let closest = closestPointOnAabb box sphere.Center
    Vector3.Distance(sphere.Center, closest) < sphere.Radius

let sphereVsAabbPenetration (sphere: BoundingSphere) (box: AABB) : float32 =
    let closest = closestPointOnAabb box sphere.Center
    sphere.Radius - Vector3.Distance(sphere.Center, closest)

let sphereVsAabbContact (sphere: BoundingSphere) (box: AABB) =
    let closest = closestPointOnAabb box sphere.Center
    let d = Vector3.Distance(sphere.Center, closest)
    let pen = sphere.Radius - d
    if pen <= 0.0f then None
    else
        let normal = Vector3.Normalize(sphere.Center - closest)
        Some (closest, normal, pen)

// ============================================================================
// OBB vs OBB — Separating Axis Theorem (15 axes)
// ============================================================================

/// Project an OBB onto an axis and return (min, max) interval
let private projectObb (obb: OBB) (axis: Vector3) : float32 * float32 =
    let c = Vector3.Dot(obb.Center, axis)
    let r =
        abs (Vector3.Dot(obb.AxisX, axis)) * obb.HalfExtents.X +
        abs (Vector3.Dot(obb.AxisY, axis)) * obb.HalfExtents.Y +
        abs (Vector3.Dot(obb.AxisZ, axis)) * obb.HalfExtents.Z
    (c - r, c + r)

/// Test overlap on a single axis, returns separation (negative = overlapping)
let private axisOverlap (a: OBB) (b: OBB) (axis: Vector3) : float32 =
    if axis.LengthSquared() < 1e-10f then infinityf  // degenerate axis, skip
    else
        let axis = Vector3.Normalize(axis)
        let aMin, aMax = projectObb a axis
        let bMin, bMax = projectObb b axis
        let overlap = min (aMax - bMin) (bMax - aMin)
        overlap

/// Test if two OBBs overlap using the Separating Axis Theorem.
/// Tests all 15 potential separating axes: 3 from A, 3 from B, 9 cross products.
let obbVsObb (a: OBB) (b: OBB) : bool =
    let axes = [
        // 3 face normals of A
        a.AxisX; a.AxisY; a.AxisZ
        // 3 face normals of B
        b.AxisX; b.AxisY; b.AxisZ
        // 9 edge-edge cross products
        Vector3.Cross(a.AxisX, b.AxisX); Vector3.Cross(a.AxisX, b.AxisY); Vector3.Cross(a.AxisX, b.AxisZ)
        Vector3.Cross(a.AxisY, b.AxisX); Vector3.Cross(a.AxisY, b.AxisY); Vector3.Cross(a.AxisY, b.AxisZ)
        Vector3.Cross(a.AxisZ, b.AxisX); Vector3.Cross(a.AxisZ, b.AxisY); Vector3.Cross(a.AxisZ, b.AxisZ)
    ]
    axes |> List.forall (fun axis -> axisOverlap a b axis > 0.0f)

/// Penetration depth and separating axis for two OBBs.
/// Returns None if not colliding, Some (depth, axis) for minimum penetration axis.
let obbVsObbPenetration (a: OBB) (b: OBB) =
    let axes = [
        a.AxisX; a.AxisY; a.AxisZ
        b.AxisX; b.AxisY; b.AxisZ
        Vector3.Cross(a.AxisX, b.AxisX); Vector3.Cross(a.AxisX, b.AxisY); Vector3.Cross(a.AxisX, b.AxisZ)
        Vector3.Cross(a.AxisY, b.AxisX); Vector3.Cross(a.AxisY, b.AxisY); Vector3.Cross(a.AxisY, b.AxisZ)
        Vector3.Cross(a.AxisZ, b.AxisX); Vector3.Cross(a.AxisZ, b.AxisY); Vector3.Cross(a.AxisZ, b.AxisZ)
    ]
    let mutable minOverlap = infinityf
    let mutable minAxis = Vector3.Zero
    let mutable separated = false
    for axis in axes do
        if not separated then
            let overlap = axisOverlap a b axis
            if overlap <= 0.0f then separated <- true
            elif overlap < minOverlap && axis.LengthSquared() >= 1e-10f then
                minOverlap <- overlap
                minAxis <- Vector3.Normalize(axis)
    if separated then None
    else
        // Ensure normal points from A to B
        let d = b.Center - a.Center
        let minAxis = if Vector3.Dot(minAxis, d) < 0.0f then -minAxis else minAxis
        Some (minOverlap, minAxis)

// ============================================================================
// Sphere vs OBB
// ============================================================================

/// Closest point on an OBB to a given point
let closestPointOnObb (obb: OBB) (point: Vector3) : Vector3 =
    let d = point - obb.Center
    let mutable result = obb.Center
    let axes = [| obb.AxisX; obb.AxisY; obb.AxisZ |]
    let halfs = [| obb.HalfExtents.X; obb.HalfExtents.Y; obb.HalfExtents.Z |]
    for i in 0..2 do
        let dist = Vector3.Dot(d, axes[i])
        let clamped = max -halfs[i] (min halfs[i] dist)
        result <- result + axes[i] * clamped
    result

/// Test if a sphere overlaps an OBB
let sphereVsObb (sphere: BoundingSphere) (obb: OBB) : bool =
    let closest = closestPointOnObb obb sphere.Center
    Vector3.Distance(sphere.Center, closest) < sphere.Radius

/// Penetration depth for sphere vs OBB
let sphereVsObbPenetration (sphere: BoundingSphere) (obb: OBB) : float32 =
    let closest = closestPointOnObb obb sphere.Center
    sphere.Radius - Vector3.Distance(sphere.Center, closest)

/// Contact info for sphere vs OBB
let sphereVsObbContact (sphere: BoundingSphere) (obb: OBB) =
    let closest = closestPointOnObb obb sphere.Center
    let d = Vector3.Distance(sphere.Center, closest)
    let pen = sphere.Radius - d
    if pen <= 0.0f then None
    else
        let normal = Vector3.Normalize(sphere.Center - closest)
        Some (closest, normal, pen)

// ============================================================================
// Point-in-shape tests
// ============================================================================

let pointInAabb (box: AABB) (point: Vector3) : bool =
    let d = Vector3.Abs(point - box.Center)
    d.X <= box.HalfExtents.X && d.Y <= box.HalfExtents.Y && d.Z <= box.HalfExtents.Z

let pointInSphere (sphere: BoundingSphere) (point: Vector3) : bool =
    Vector3.Distance(sphere.Center, point) <= sphere.Radius

let pointInObb (obb: OBB) (point: Vector3) : bool =
    let d = point - obb.Center
    abs (Vector3.Dot(d, obb.AxisX)) <= obb.HalfExtents.X &&
    abs (Vector3.Dot(d, obb.AxisY)) <= obb.HalfExtents.Y &&
    abs (Vector3.Dot(d, obb.AxisZ)) <= obb.HalfExtents.Z

// ============================================================================
// Ray intersection tests
// ============================================================================

/// Ray-sphere intersection. Returns Some t (distance along ray) or None.
let raySphere (origin: Vector3) (dir: Vector3) (sphere: BoundingSphere) : float32 option =
    let oc = origin - sphere.Center
    let a = Vector3.Dot(dir, dir)
    let b = 2.0f * Vector3.Dot(oc, dir)
    let c = Vector3.Dot(oc, oc) - sphere.Radius * sphere.Radius
    let discriminant = b * b - 4.0f * a * c
    if discriminant < 0.0f then None
    else
        let t = (-b - sqrt discriminant) / (2.0f * a)
        if t >= 0.0f then Some t
        else
            let t2 = (-b + sqrt discriminant) / (2.0f * a)
            if t2 >= 0.0f then Some t2 else None

/// Ray-AABB intersection (slab method). Returns Some (tmin, tmax) or None.
let rayAabb (origin: Vector3) (dir: Vector3) (box: AABB) : (float32 * float32) option =
    let inline slabTest (o: float32) (d: float32) (lo: float32) (hi: float32) =
        if abs d < 1e-12f then
            if o < lo || o > hi then (infinityf, -infinityf)
            else (-infinityf, infinityf)
        else
            let t1 = (lo - o) / d
            let t2 = (hi - o) / d
            (min t1 t2, max t1 t2)
    let bMin = box.Min
    let bMax = box.Max
    let txMin, txMax = slabTest origin.X dir.X bMin.X bMax.X
    let tyMin, tyMax = slabTest origin.Y dir.Y bMin.Y bMax.Y
    let tzMin, tzMax = slabTest origin.Z dir.Z bMin.Z bMax.Z
    let tmin = max txMin (max tyMin tzMin)
    let tmax = min txMax (min tyMax tzMax)
    if tmax >= tmin && tmax >= 0.0f then Some (max tmin 0.0f, tmax)
    else None

/// Ray-OBB intersection. Transforms ray into OBB local space, then uses slab method.
let rayObb (origin: Vector3) (dir: Vector3) (obb: OBB) : (float32 * float32) option =
    let d = origin - obb.Center
    let localOrigin = Vector3(Vector3.Dot(d, obb.AxisX), Vector3.Dot(d, obb.AxisY), Vector3.Dot(d, obb.AxisZ))
    let localDir = Vector3(Vector3.Dot(dir, obb.AxisX), Vector3.Dot(dir, obb.AxisY), Vector3.Dot(dir, obb.AxisZ))
    let localBox = { Center = Vector3.Zero; HalfExtents = obb.HalfExtents }
    rayAabb localOrigin localDir localBox

// ============================================================================
// Scene scanning helpers (require PClientV2 Session and Vec3)
// ============================================================================

#r "nuget: PClientV2, *-*"

open PhysicsClient.Session
open PhysicsSandbox.Shared.Contracts
open PClientV2

/// Scan a rectangular region with downward raycasts to find body positions.
let scanBodies (session: Session) (xMin: float) (xMax: float) (zMin: float) (zMax: float) (step: float) : (string * Vec3) list =
    [ for x in xMin .. step .. xMax do
        for z in zMin .. step .. zMax do
            for (id, pos, _, _) in raycast session (vec3 (x, 50.0, z)) (vec3 (0.0, -1.0, 0.0)) 100.0 do
                if id <> "plane-1" then yield (id, pos) ]
    |> List.distinctBy fst

/// Find a specific body by scanning with raycasts
let findBody (session: Session) (bodyId: string) (xMin: float) (xMax: float) (zMin: float) (zMax: float) (step: float) : Vec3 option =
    scanBodies session xMin xMax zMin zMax step
    |> List.tryFind (fun (id, _) -> id = bodyId)
    |> Option.map snd

/// Get all body IDs in a region (using overlapSphere)
let bodiesInRadius (session: Session) (cx: float) (cy: float) (cz: float) (radius: float) : string list =
    overlapSphere session radius (vec3 (cx, cy, cz))
    |> List.filter (fun id -> id <> "plane-1")

/// Create a custom body with an explicit ID (avoids ID counter collision issues)
let makeCustomBody (id: string) (x: float, y: float, z: float) (shape: Shape) (mass: float) : SimulationCommand =
    let cmd = SimulationCommand()
    cmd.AddBody <- AddBody(Id = id, Position = Vec3(X = x, Y = y, Z = z), Mass = mass, Shape = shape)
    cmd

let makeCustomSphere (id: string) (x: float, y: float, z: float) (radius: float) (mass: float) : SimulationCommand =
    makeCustomBody id (x, y, z) (Shape(Sphere = Sphere(Radius = radius))) mass

let makeCustomBox (id: string) (x: float, y: float, z: float) (halfX: float, halfY: float, halfZ: float) (mass: float) : SimulationCommand =
    makeCustomBody id (x, y, z) (Shape(Box = Box(HalfExtents = Vec3(X = halfX, Y = halfY, Z = halfZ)))) mass

/// Aim and fire: create a sphere at fromPos and apply impulse toward toPos
let fireAt (session: Session) (ballId: string) (fromPos: float * float * float) (radius: float) (mass: float) (toPos: float * float * float) (speed: float) : unit =
    batchAdd session [makeCustomSphere ballId fromPos radius mass]
    let dir = v3 fromPos |> fun f -> direction f (v3 toPos)
    let imp = impulseForVelocity (float32 mass) (float32 speed)
    let impulse = dir * imp
    batchAdd session [makeImpulseCmd (ballId, Vec3(X = float impulse.X, Y = float impulse.Y, Z = float impulse.Z))]
