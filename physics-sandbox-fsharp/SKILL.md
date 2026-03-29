---
name: physics-sandbox-fsharp
description: >-
  Use when the user asks to "create a physics simulation", "throw a ball", "build a scene",
  "use PhysicsSandbox", "script physics in F#", "connect to physics server",
  "create bodies/spheres/boxes", "apply forces/impulses", "set up constraints",
  "run a physics experiment", or needs to work with PhysicsSandbox via the PClientV2 F# scripting library.
  Also use when the user is working in a .fsx file that references PClientV2 or PhysicsClient,
  or when using the FSI MCP server to interact with a physics simulation.
version: 1.0.0
---

# PhysicsSandbox F# Scripting Skill

Script real-time physics simulations using [PClientV2](https://github.com/yourusername/PClientV2),
an F# library for the PhysicsSandbox physics engine. Supports interactive use via FSI (F# Interactive)
and the FSI MCP server.

## Quick Start

### Script boilerplate

```fsharp
#r "nuget: PClientV2, *-*"

open PhysicsClient.Session
open PhysicsSandbox.Shared.Contracts
open PClientV2

let s = PhysicsClient.Session.connect "http://localhost:5180" |> ok
resetSimulation s
```

### FSI MCP usage

When using the `fsi-server` MCP tools, send code via `mcp__fsi-server__send_fsharp_code`.
Terminate all statements with `;;`. Load scripts with `mcp__fsi-server__load_f_sharp_script`.

## Complete API Reference

### Connection & Lifecycle

| Function | Signature | Description |
|----------|-----------|-------------|
| `PhysicsClient.Session.connect` | `string -> Result<Session, string>` | Connect to PhysicsSandbox server |
| `resetSimulation` | `Session -> unit` | Reset sim: clear bodies, add ground plane, set gravity (-9.81), reset ID counter |
| `runFor` | `Session -> seconds:float -> unit` | Run simulation for N seconds at 60 Hz |
| `nextId` | `prefix:string -> string` | Generate sequential ID (e.g. `nextId "sphere"` -> `"sphere-1"`) |

### State Access (requires PhysicsClient 0.7.0+)

| Function | Signature | Description |
|----------|-----------|-------------|
| `latestState` | `Session -> SimulationState option` | Full simulation state: all bodies with position, velocity, orientation, mass, shape |
| `bodyPositions` | `Session -> (string * Vec3) list` | All body positions from latest state |
| `getBody` | `Session -> bodyId:string -> Body option` | Look up a specific body by ID |
| `bodyRegistry` | `Session -> ConcurrentDictionary<string, string>` | Body ID to shape kind mapping |
| `lastStateUpdate` | `Session -> DateTime` | Timestamp of last state update from server |

**`Body` fields:** `Id`, `Position`, `Velocity`, `Mass`, `Shape`, `AngularVelocity`, `Orientation`, `IsStatic`, `Color`, `MotionType`, `CollisionGroup`, `CollisionMask`, `Material`

**`SimulationState` fields:** `Bodies` (repeated Body), `Time`, `Running`, `TickMs`, `SerializeMs`

### Body Creation Commands

| Function | Signature | Description |
|----------|-----------|-------------|
| `makeSphereCmd` | `x * y * z * radius * mass -> SimulationCommand` | Create sphere body |
| `makeBoxCmd` | `x * y * z * halfX * halfY * halfZ * mass -> SimulationCommand` | Create box body (half-extents) |
| `makeCapsuleCmd` | `x * y * z * radius * length * mass -> SimulationCommand` | Create capsule body |
| `makeCylinderCmd` | `x * y * z * radius * length * mass -> SimulationCommand` | Create cylinder body |

All parameters are `float`. Mass of `0.0` creates a static (immovable) body.

### Force & Motion Commands

| Function | Signature | Description |
|----------|-----------|-------------|
| `makeImpulseCmd` | `bodyId:string * direction:Vec3 -> SimulationCommand` | Apply instantaneous impulse |
| `makeTorqueCmd` | `bodyId:string * torque:Vec3 -> SimulationCommand` | Apply angular torque |
| `makeSetBodyPoseCmd` | `bodyId:string * position:Vec3 * rotation:Vec4 -> SimulationCommand` | Teleport body |

### Constraint Commands

| Function | Signature | Description |
|----------|-----------|-------------|
| `makeBallSocketCmd` | `bodyA * bodyB * pivot:Vec3` | Universal joint at pivot |
| `makeHingeCmd` | `bodyA * bodyB * pivot:Vec3 * axis:Vec3` | Single-axis rotation |
| `makeWeldCmd` | `bodyA * bodyB * pivot:Vec3` | Rigid attachment |
| `makeDistanceLimitCmd` | `bodyA * bodyB * min * max` | Keep distance in [min, max] |
| `makeDistanceSpringCmd` | `bodyA * bodyB * length * frequency * damping` | Spring connection |
| `makeLinearAxisMotorCmd` | `bodyA * bodyB * axis:Vec3 * speed * force` | Linear motor |
| `makeAngularMotorCmd` | `bodyA * bodyB * axis:Vec3 * speed * force` | Rotational motor |
| `makeRemoveConstraintCmd` | `constraintId:string` | Remove constraint by ID |

### Query Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `raycast` | `Session -> origin:Vec3 -> direction:Vec3 -> maxDistance:float -> (bodyId * pos * normal * dist) list` | Cast ray, return all hits |
| `sweepSphere` | `Session -> radius -> origin:Vec3 -> direction:Vec3 -> maxDistance -> ... option` | Sweep sphere, return closest hit |
| `overlapSphere` | `Session -> radius:float -> center:Vec3 -> string list` | Find all body IDs in sphere |

### Materials & Colors

| Value/Function | Description |
|----------------|-------------|
| `bouncyMaterial` | Friction=0.4, Recovery=8.0, Spring=30.0 |
| `stickyMaterial` | Friction=2.0, Recovery=1.0, Spring=30.0 |
| `slipperyMaterial` | Friction=0.01, Recovery=4.0, Spring=30.0 |
| `makeMaterialProperties` | `friction -> recovery -> spring -> MaterialProperties` |
| `makeColor` | `r -> g -> b -> a -> Color` |

### Vec3 Builders

| Value | Vector |
|-------|--------|
| `vec3 (x, y, z)` | Custom vector |
| `origin` | (0, 0, 0) |
| `up` / `down` | (0, 1, 0) / (0, -1, 0) |
| `north` / `south` | (0, 0, -1) / (0, 0, 1) |
| `east` / `west` | (1, 0, 0) / (-1, 0, 0) |

### Batch Operations

| Function | Signature | Description |
|----------|-----------|-------------|
| `batchAdd` | `Session -> SimulationCommand list -> unit` | Send commands in batches of 100 |

### Helpers

| Function | Signature | Description |
|----------|-----------|-------------|
| `ok` | `Result<'a, string> -> 'a` | Unwrap Result or throw |
| `sleep` | `int -> unit` | Sleep for N milliseconds |
| `timed` | `string -> ('a -> 'b) -> 'a -> 'b` | Time and log a function call |

## Known Issues & Workarounds

### ID collision after resetSimulation

`resetSimulation` resets the client-side ID counter, but if the server hasn't finished clearing
bodies (fire-and-forget command), new bodies with auto-generated IDs like `sphere-1` may collide
with stale server-side bodies and silently fail to create.

**Workaround:** Use custom unique IDs instead of `makeSphereCmd`:

```fsharp
let makeCustomBody id (x, y, z) shape mass =
    let cmd = SimulationCommand()
    cmd.AddBody <- AddBody(Id = id, Position = Vec3(X = x, Y = y, Z = z), Mass = mass, Shape = shape)
    cmd

let makeCustomSphere id (x, y, z) radius mass =
    makeCustomBody id (x, y, z) (Shape(Sphere = Sphere(Radius = radius))) mass

batchAdd s [makeCustomSphere "my-ball" (0.0, 5.0, 0.0) 1.0 10.0]
```

**Fix available:** PhysicsSandbox branch `005-fix-session-state-sync` adds `ConfirmedReset` RPC
that blocks until reset completes. After updating PhysicsClient, auto-generated IDs work correctly.

### Reading body positions

With PhysicsClient 0.7.0+, use the state access API directly:

```fsharp
// All body positions
let positions = bodyPositions s
for (id, pos) in positions do
    printfn "%s -> (%.2f, %.2f, %.2f)" id pos.X pos.Y pos.Z

// Single body with full state (position, velocity, orientation, etc.)
match getBody s "sphere-1" with
| Some body -> printfn "pos=%A vel=%A" body.Position body.Velocity
| None -> printfn "not found"

// Full simulation state
match latestState s with
| Some state -> printfn "Time=%.2f Bodies=%d Running=%b" state.Time state.Bodies.Count state.Running
| None -> printfn "no state yet"
```

For spatial queries (what's at this position?), raycasts and overlap are still useful:

```fsharp
let findAt x z =
    raycast s (vec3 (x, 50.0, z)) (vec3 (0.0, -1.0, 0.0)) 100.0
    |> List.filter (fun (id, _, _, _) -> id <> "plane-1")
```

## Physics Helper Library

A companion helper script is available at:
`/home/developer/projects/PhysicsSkill/physics-helpers.fsx`

This provides (all vector math uses `System.Numerics.Vector3`):
- **Estimation functions**: Calculate impulse needed for a target velocity, projectile trajectories,
  time to fall from height, impact velocity, kinetic energy
- **Collision detection**: Sphere-sphere, AABB-AABB, sphere-AABB, OBB-OBB (SAT, 15 axes),
  sphere-OBB, ray-sphere, ray-AABB, ray-OBB
- **OBB construction**: From quaternion, Euler angles, or axis-aligned
- **Scene scanning**: Grid-based raycast scanning to map body positions

Load it in scripts with:
```fsharp
#load "/home/developer/projects/PhysicsSkill/physics-helpers.fsx"
open PhysicsHelpers
```

## Common Patterns

### Throw a ball at targets

```fsharp
// 1. Create targets and settle
batchAdd s [
    makeSphereCmd (5.0, 2.0, 0.0, 0.5, 5.0)
    makeSphereCmd (6.0, 2.0, 0.0, 0.5, 5.0)
    makeSphereCmd (5.5, 2.0, 1.0, 0.5, 5.0)
]
runFor s 2.0

// 2. Get target positions directly from state
let targets = bodyPositions s |> List.filter (fun (id, _) -> id <> "plane-1")
let cx = targets |> List.averageBy (fun (_, p) -> p.X)
let cy = targets |> List.averageBy (fun (_, p) -> p.Y)
let cz = targets |> List.averageBy (fun (_, p) -> p.Z)

// 3. Create and launch ball
let ballX = cx - 10.0
batchAdd s [makeCustomSphere "ball" (ballX, cy, cz) 0.8 30.0]
let speed = 15.0
let imp = 30.0 * speed  // mass * target_speed
let dist = cx - ballX
batchAdd s [makeImpulseCmd ("ball", Vec3(X = imp, Y = 0.0, Z = 0.0))]
runFor s 4.0
```

### Build a wall of boxes

```fsharp
let wall rows cols spacing y0 =
    [ for r in 0 .. rows - 1 do
        for c in 0 .. cols - 1 do
            let x = float c * spacing - float (cols - 1) * spacing / 2.0
            let y = y0 + float r * spacing
            makeBoxCmd (x, y, 0.0, spacing/2.2, spacing/2.2, spacing/2.2, 5.0) ]

batchAdd s (wall 5 4 1.1 0.55)
runFor s 1.0
```

### Pendulum with constraint

```fsharp
// Static anchor + dynamic bob connected by ball socket
batchAdd s [
    makeSphereCmd (0.0, 8.0, 0.0, 0.2, 0.0)  // static anchor (mass=0)
    makeSphereCmd (3.0, 8.0, 0.0, 0.5, 5.0)   // pendulum bob
]
batchAdd s [makeBallSocketCmd ("sphere-1", "sphere-2", vec3 (0.0, 8.0, 0.0))]
runFor s 10.0
```

## Coordinate System

- **Y is up** (gravity is -Y)
- Ground plane is at Y = 0
- Default gravity: (0, -9.81, 0)
- Simulation runs at 60 Hz (`runFor s 1.0` = 60 steps)

## Typical Body Scales

| Object | Radius/Size | Mass | Notes |
|--------|------------|------|-------|
| Small ball | r=0.2-0.5 | 1-5 | Tennis ball to bowling ball |
| Large ball | r=0.8-1.5 | 10-50 | Wrecking ball / cannonball |
| Small box | half=0.25-0.5 | 2-8 | Brick-sized |
| Large box | half=1.0-2.0 | 20-100 | Crate-sized |
| Person-scale capsule | r=0.3, l=1.5 | 70 | Roughly human-shaped |

## Impulse Guidelines

- Impulse = mass * velocity_change (kg*m/s)
- A 10kg ball needs impulse=100 for 10 m/s speed
- Gentle push: impulse ~20-50
- Strong throw: impulse ~100-300
- Cannon shot: impulse ~500+
- Too much impulse sends objects out of the scene
