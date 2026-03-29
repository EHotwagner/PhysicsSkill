/// PhysicsHelpers — Estimation, collision detection, and utility functions
/// for PhysicsSandbox F# scripting.
///
/// Usage:
///   #load "/home/developer/projects/PhysicsSkill/physics-helpers.fsx"
///   open PhysicsHelpers
module PhysicsHelpers

// ============================================================================
// Tuple helpers for (x, y, z)
// ============================================================================

let fst3 (a, _, _) = a
let snd3 (_, b, _) = b
let thd3 (_, _, c) = c

// ============================================================================
// Vector math (works with float tuples to avoid dependency on Vec3)
// ============================================================================

let add3 (x1, y1, z1) (x2, y2, z2) = (x1 + x2, y1 + y2, z1 + z2)
let sub3 (x1, y1, z1) (x2, y2, z2) = (x1 - x2, y1 - y2, z1 - z2)
let scale3 s (x, y, z) = (s * x, s * y, s * z)
let dot3 (x1, y1, z1) (x2, y2, z2) = x1 * x2 + y1 * y2 + z1 * z2
let length3 v = sqrt (dot3 v v)
let distance3 a b = length3 (sub3 b a)

let normalize3 v =
    let m = length3 v
    if m < 1e-12 then (0.0, 0.0, 0.0)
    else scale3 (1.0 / m) v

let direction3 fromPt toPt = normalize3 (sub3 toPt fromPt)

let cross3 (x1, y1, z1) (x2, y2, z2) =
    (y1 * z2 - z1 * y2, z1 * x2 - x1 * z2, x1 * y2 - y1 * x2)

let midpoint3 a b = scale3 0.5 (add3 a b)

let centroid (pts: (float * float * float) list) =
    let n = float pts.Length
    let sx = pts |> List.sumBy fst3
    let sy = pts |> List.sumBy snd3
    let sz = pts |> List.sumBy thd3
    (sx / n, sy / n, sz / n)

// ============================================================================
// Physics estimation — forces, impulses, projectiles
// ============================================================================

let gravity = 9.81

/// Impulse needed to achieve a target velocity: impulse = mass * velocity
let impulseForVelocity (mass: float) (targetSpeed: float) : float =
    mass * targetSpeed

/// Time for an object to fall from height h (starting from rest): t = sqrt(2h/g)
let fallTime (height: float) : float =
    if height <= 0.0 then 0.0
    else sqrt (2.0 * height / gravity)

/// Velocity after falling from height h: v = sqrt(2gh)
let fallVelocity (height: float) : float =
    if height <= 0.0 then 0.0
    else sqrt (2.0 * gravity * height)

/// Max height reached when launched upward at speed v: h = v^2 / (2g)
let maxHeight (upwardSpeed: float) : float =
    upwardSpeed * upwardSpeed / (2.0 * gravity)

/// Kinetic energy: KE = 0.5 * m * v^2
let kineticEnergy (mass: float) (speed: float) : float =
    0.5 * mass * speed * speed

/// Momentum: p = m * v
let momentum (mass: float) (speed: float) : float =
    mass * speed

/// Projectile range on flat ground (no air resistance)
/// launchSpeed: initial speed, launchAngleDeg: angle above horizontal
let projectileRange (launchSpeed: float) (launchAngleDeg: float) : float =
    let rad = launchAngleDeg * System.Math.PI / 180.0
    launchSpeed * launchSpeed * sin (2.0 * rad) / gravity

/// Projectile max height
let projectileMaxHeight (launchSpeed: float) (launchAngleDeg: float) : float =
    let rad = launchAngleDeg * System.Math.PI / 180.0
    let vy = launchSpeed * sin rad
    vy * vy / (2.0 * gravity)

/// Projectile flight time (total, flat ground)
let projectileFlightTime (launchSpeed: float) (launchAngleDeg: float) : float =
    let rad = launchAngleDeg * System.Math.PI / 180.0
    2.0 * launchSpeed * sin rad / gravity

/// Position of projectile at time t, given launch position, speed, angle (XY plane)
/// Returns (x, y) displacement from launch point
let projectilePositionAt (launchSpeed: float) (launchAngleDeg: float) (t: float) : float * float =
    let rad = launchAngleDeg * System.Math.PI / 180.0
    let vx = launchSpeed * cos rad
    let vy = launchSpeed * sin rad
    (vx * t, vy * t - 0.5 * gravity * t * t)

/// Impulse vector to launch a projectile from origin toward target position
/// Returns (ix, iy, iz) impulse components
let impulseToHit (mass: float) (fromPos: float * float * float) (toPos: float * float * float) (flightTime: float) : float * float * float =
    let dx = fst3 toPos - fst3 fromPos
    let dy = snd3 toPos - snd3 fromPos
    let dz = thd3 toPos - thd3 fromPos
    // Required velocity: v = d/t, but account for gravity on Y
    let vx = dx / flightTime
    let vy = (dy + 0.5 * gravity * flightTime * flightTime) / flightTime
    let vz = dz / flightTime
    (mass * vx, mass * vy, mass * vz)

/// Estimate impulse for a direct straight-line throw (ignoring gravity arc)
let impulseDirectThrow (mass: float) (fromPos: float * float * float) (toPos: float * float * float) (speed: float) : float * float * float =
    let dir = direction3 fromPos toPos
    let imp = impulseForVelocity mass speed
    scale3 imp dir

// ============================================================================
// Collision detection — analytical tests between primitive shapes
// ============================================================================

/// Axis-Aligned Bounding Box: center position + half-extents
type AABB =
    { Center: float * float * float
      HalfExtents: float * float * float }

/// Sphere: center position + radius
type BoundingSphere =
    { Center: float * float * float
      Radius: float }

// -- Sphere vs Sphere --

/// Test if two spheres overlap
let sphereVsSphere (a: BoundingSphere) (b: BoundingSphere) : bool =
    let d = distance3 a.Center b.Center
    d < a.Radius + b.Radius

/// Penetration depth between two spheres (negative = separated)
let sphereVsSpherePenetration (a: BoundingSphere) (b: BoundingSphere) : float =
    let d = distance3 a.Center b.Center
    (a.Radius + b.Radius) - d

/// Contact point and normal for sphere-sphere collision
/// Returns None if not colliding
let sphereVsSphereContact (a: BoundingSphere) (b: BoundingSphere) =
    let d = distance3 a.Center b.Center
    let pen = (a.Radius + b.Radius) - d
    if pen <= 0.0 then None
    else
        let normal = direction3 a.Center b.Center
        let contact = add3 a.Center (scale3 a.Radius normal)
        Some (contact, normal, pen)

// -- AABB vs AABB --

/// Test if two AABBs overlap
let aabbVsAabb (a: AABB) (b: AABB) : bool =
    let ax1, ay1, az1 = sub3 a.Center a.HalfExtents
    let ax2, ay2, az2 = add3 a.Center a.HalfExtents
    let bx1, by1, bz1 = sub3 b.Center b.HalfExtents
    let bx2, by2, bz2 = add3 b.Center b.HalfExtents
    ax1 <= bx2 && ax2 >= bx1 &&
    ay1 <= by2 && ay2 >= by1 &&
    az1 <= bz2 && az2 >= bz1

/// Penetration depth on each axis for two AABBs
/// Returns None if not colliding, Some (px, py, pz) with min-axis penetration
let aabbVsAabbPenetration (a: AABB) (b: AABB) =
    let dx = (fst3 a.HalfExtents + fst3 b.HalfExtents) - abs (fst3 a.Center - fst3 b.Center)
    let dy = (snd3 a.HalfExtents + snd3 b.HalfExtents) - abs (snd3 a.Center - snd3 b.Center)
    let dz = (thd3 a.HalfExtents + thd3 b.HalfExtents) - abs (thd3 a.Center - thd3 b.Center)
    if dx <= 0.0 || dy <= 0.0 || dz <= 0.0 then None
    else Some (dx, dy, dz)

// -- Sphere vs AABB --

/// Closest point on an AABB to a given point
let closestPointOnAabb (box: AABB) (point: float * float * float) : float * float * float =
    let clamp lo hi v = max lo (min hi v)
    let minX = fst3 box.Center - fst3 box.HalfExtents
    let maxX = fst3 box.Center + fst3 box.HalfExtents
    let minY = snd3 box.Center - snd3 box.HalfExtents
    let maxY = snd3 box.Center + snd3 box.HalfExtents
    let minZ = thd3 box.Center - thd3 box.HalfExtents
    let maxZ = thd3 box.Center + thd3 box.HalfExtents
    (clamp minX maxX (fst3 point),
     clamp minY maxY (snd3 point),
     clamp minZ maxZ (thd3 point))

/// Test if a sphere overlaps an AABB
let sphereVsAabb (sphere: BoundingSphere) (box: AABB) : bool =
    let closest = closestPointOnAabb box sphere.Center
    let d = distance3 sphere.Center closest
    d < sphere.Radius

/// Penetration depth for sphere vs AABB (negative = separated)
let sphereVsAabbPenetration (sphere: BoundingSphere) (box: AABB) : float =
    let closest = closestPointOnAabb box sphere.Center
    let d = distance3 sphere.Center closest
    sphere.Radius - d

/// Contact info for sphere vs AABB
let sphereVsAabbContact (sphere: BoundingSphere) (box: AABB) =
    let closest = closestPointOnAabb box sphere.Center
    let d = distance3 sphere.Center closest
    let pen = sphere.Radius - d
    if pen <= 0.0 then None
    else
        let normal = direction3 closest sphere.Center
        Some (closest, normal, pen)

// -- Point tests --

/// Test if a point is inside an AABB
let pointInAabb (box: AABB) (point: float * float * float) : bool =
    abs (fst3 point - fst3 box.Center) <= fst3 box.HalfExtents &&
    abs (snd3 point - snd3 box.Center) <= snd3 box.HalfExtents &&
    abs (thd3 point - thd3 box.Center) <= thd3 box.HalfExtents

/// Test if a point is inside a sphere
let pointInSphere (sphere: BoundingSphere) (point: float * float * float) : bool =
    distance3 sphere.Center point <= sphere.Radius

// -- Ray tests --

/// Ray-sphere intersection. Returns Some t (distance along ray) or None
let raySphere (origin: float * float * float) (dir: float * float * float) (sphere: BoundingSphere) : float option =
    let oc = sub3 origin sphere.Center
    let a = dot3 dir dir
    let b = 2.0 * dot3 oc dir
    let c = dot3 oc oc - sphere.Radius * sphere.Radius
    let discriminant = b * b - 4.0 * a * c
    if discriminant < 0.0 then None
    else
        let t = (-b - sqrt discriminant) / (2.0 * a)
        if t >= 0.0 then Some t
        else
            let t2 = (-b + sqrt discriminant) / (2.0 * a)
            if t2 >= 0.0 then Some t2 else None

/// Ray-AABB intersection (slab method). Returns Some (tmin, tmax) or None
let rayAabb (origin: float * float * float) (dir: float * float * float) (box: AABB) : (float * float) option =
    let inline slabTest o d ctr half =
        if abs d < 1e-12 then
            if abs (o - ctr) > half then (infinity, -infinity)
            else (-infinity, infinity)
        else
            let t1 = (ctr - half - o) / d
            let t2 = (ctr + half - o) / d
            (min t1 t2, max t1 t2)
    let (txMin, txMax) = slabTest (fst3 origin) (fst3 dir) (fst3 box.Center) (fst3 box.HalfExtents)
    let (tyMin, tyMax) = slabTest (snd3 origin) (snd3 dir) (snd3 box.Center) (snd3 box.HalfExtents)
    let (tzMin, tzMax) = slabTest (thd3 origin) (thd3 dir) (thd3 box.Center) (thd3 box.HalfExtents)
    let tmin = max txMin (max tyMin tzMin)
    let tmax = min txMax (min tyMax tzMax)
    if tmax >= tmin && tmax >= 0.0 then Some (max tmin 0.0, tmax)
    else None

// ============================================================================
// Scene scanning helpers (require PClientV2 Session and Vec3)
// ============================================================================

#r "nuget: PClientV2, *-*"

open PhysicsClient.Session
open PhysicsSandbox.Shared.Contracts
open PClientV2

/// Scan a rectangular region with downward raycasts to find body positions.
/// Returns list of (bodyId, hitPosition) excluding the ground plane.
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
    let dir = direction3 fromPos toPos
    let imp = impulseForVelocity mass speed
    let ix, iy, iz = scale3 imp dir
    batchAdd session [makeImpulseCmd (ballId, Vec3(X = ix, Y = iy, Z = iz))]
