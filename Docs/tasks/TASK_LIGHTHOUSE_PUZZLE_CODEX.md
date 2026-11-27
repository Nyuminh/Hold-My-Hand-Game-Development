# 📋 TASK SPECIFICATION: LIGHTHOUSE PUZZLE IMPLEMENTATION (PLAYSCENE2)
> **Target Agent / Developer**: OpenAI Codex / Developer  
> **Project**: Hold My Hand · Nắm tay cha (Unity 6 / Netcode for GameObjects)  
> **Scene**: `Assets/Core/Scenes/PlayScene2.unity` (Stage 4: Misty Marsh & Twin Beacons)  
> **Feature**: Light Refraction & Item Carrying Cooperative Lighthouse Puzzle

---

## 🎯 OBJECTIVE SUMMARY
Replace the simplistic lily-pad throw trigger at the Twin Lighthouses (West Beacon & East Beacon in PlayScene2, Z: 155–215) with an asymmetric cooperative light-refraction puzzle involving item pickup/carrying and coordinated beam alignment between Father (Ba) and Child (Con).

---

## 📚 CONTEXT FILES TO READ FIRST
Before modifying or creating code, read:
1. `Assets/Core/Scripts/Runtime/Components/MarshBeaconTrigger.cs` (Current beacon trigger)
2. `Assets/Core/Scripts/Runtime/Components/GiantWatermillObstacle.cs` (Controls safety boom & checks `BeaconsLit`)
3. `Assets/Core/Scripts/Runtime/Components/LevelTwoClimaxManager.cs` (Fog & progression)
4. `Assets/Core/Scripts/Runtime/Components/LevelTwoActors.cs` (Player query utilities)
5. `Assets/Core/Scripts/Runtime/Components/CoopCrawlVolume.cs` (Child-only crawl zones)
6. `Assets/Core/Scripts/Editor/LevelTwoSceneBuilder.cs` (Authoring script for Level 2)
7. `Assets/Core/Scripts/Editor/LevelTwoValidation.cs` (Validation test runner)

---

## 🧩 DETAILED SPECIFICATIONS & ARCHITECTURE

### 1. `CoopCarryableItem.cs` [NEW]
**Namespace**: `Blocks.Gameplay.Core`  
**Location**: `Assets/Core/Scripts/Runtime/Components/CoopCarryableItem.cs`  
**Base Class**: `NetworkBehaviour`  
**Requires**: `NetworkObject`, `Collider`

#### Fields & NetworkVariables:
- `public enum ItemType { IgnitionCrank, RefractionPrism }`
- `[SerializeField] public ItemType itemType;`
- `[SerializeField] public Transform carryOffset;` (Local offset when attached to player)
- `[SerializeField] public float heavyMovePenalty = 0.8f;` (Optional speed multiplier if heavy)
- `private readonly NetworkVariable<NetworkObjectReference> currentHolder = new();`
- `private readonly NetworkVariable<bool> isSocketed = new();`
- `public bool IsCarried => currentHolder.Value.TryGet(out _);`
- `public bool IsSocketed => isSocketed.Value;`

#### Methods & RPCs:
- `public bool TryPickup(CorePlayerManager player)`:
  - Check distance (`< 2.0f`).
  - If server: set `currentHolder.Value = player.NetworkObject`, disable physics collider, attach transform to player's right hand or chest anchor.
  - RPC to notify clients: `PickupClientRpc(player.NetworkObjectId)`.
- `public void Drop()`:
  - If server: detach transform, enable collider, set `currentHolder.Value = default`.
  - RPC to notify clients: `DropClientRpc()`.
- `public void InsertIntoSocket(Transform socketTransform)`:
  - If server: `isSocketed.Value = true; currentHolder.Value = default;`
  - Snap position/rotation to socket.
- Input handling:
  - When local player is within 2m and presses `Keyboard.current.eKey.wasPressedThisFrame`:
    - If not holding anything -> `RequestPickupRpc()`
    - If holding this item and near a valid socket -> `RequestSocketRpc()`
    - If holding this item and presses `Keyboard.current.gKey.wasPressedThisFrame` -> `RequestDropRpc()`

---

### 2. `LighthouseLightBeam.cs` [NEW]
**Namespace**: `Blocks.Gameplay.Core`  
**Location**: `Assets/Core/Scripts/Runtime/Components/LighthouseLightBeam.cs`  
**Base Class**: `NetworkBehaviour`  
**Requires**: `LineRenderer`

#### Functionality:
- Visual beam using `LineRenderer` (width ~0.35m, warm yellow/golden emission material `Default-Line.mat` or lit unlit material).
- Real-time `Physics.Raycast(transform.position, transform.forward, out hit, maxDistance, layerMask)`.
- **Primary Beam (West Tower)**:
  - Casts from West Beacon emitter towards East Beacon island.
  - If raycast hits `LighthousePrismSocket` on East Tower (and Prism is socketed):
    - Activates secondary beam on East Tower!
    - Calls `NotifyBeamHit(true)` on East Prism socket.
- **Secondary Beam (East Tower)**:
  - Originates from Prism socket.
  - Casts along East Tower's rotated heading towards the river / Watermill.
  - If raycast hits `LuminaMillSensor` on `GiantWatermillObstacle`:
    - Calls `NotifySensorActivated(true)`.
    - Both beacons lit condition met!

---

### 3. `LighthouseTurnWheel.cs` [NEW]
**Namespace**: `Blocks.Gameplay.Core`  
**Location**: `Assets/Core/Scripts/Runtime/Components/LighthouseTurnWheel.cs`  
**Base Class**: `NetworkBehaviour`

#### Functionality:
- Placed on the circular platform of the lighthouse.
- Allows players standing near it (within 2m) to press **[E]** to enter steering mode.
- In steering mode:
  - **A / D** or **Left / Right arrow keys** rotate the lighthouse lantern head (`NetworkVariable<float> currentAngle`).
  - Pressing **[E]** or **[Space]** releases control.
  - West Tower yaw range: clamped between $-30^\circ$ and $+30^\circ$ facing East Tower.
  - East Tower yaw range: clamped between $-45^\circ$ and $+45^\circ$ aiming downstream at the watermill sensor.

---

### 4. `LuminaMillSensor.cs` [NEW]
**Namespace**: `Blocks.Gameplay.Core`  
**Location**: `Assets/Core/Scripts/Runtime/Components/LuminaMillSensor.cs`  
**Base Class**: `NetworkBehaviour`

#### Functionality:
- Placed on `GiantWatermill_Assembly` or near the safety boom arch.
- Contains a glowing jewel/lens mesh (`Renderer` with emission color: Red when unpowered, Cyan/Gold when powered).
- When hit by the refracted beam for $\ge 0.5$ seconds:
  - `NetworkVariable<bool> isPowered = true;`
  - Triggers `MarshBeaconTrigger` west & east to `lit.Value = true;`
  - Plays chime audio (`Assets/Core/Audio/LVL2_Chime.wav`).

---

### 5. `MarshBeaconTrigger.cs` [MODIFY]
**Location**: `Assets/Core/Scripts/Runtime/Components/MarshBeaconTrigger.cs`

#### Updates:
- Wire new references:
  - `public CoopCarryableItem requiredItem;`
  - `public Transform itemSocket;`
  - `public LighthouseLightBeam beam;`
  - `public LighthouseTurnWheel turnWheel;`
- Keep existing public properties:
  - `public bool Lit => lit.Value;` (to ensure 100% backward compatibility with `GiantWatermillObstacle` and `LevelTwoClimaxManager`).
  - `public void RestoreCheckpoint(bool value)` (keep intact for checkpoint loads).

---

### 6. `LevelTwoSceneBuilder.cs` [MODIFY]
**Location**: `Assets/Core/Scripts/Editor/LevelTwoSceneBuilder.cs`

#### Update `WireStageThree()`:
1. **West Beacon (Left Island)**:
   - Place `IgnitionCrank` on a rocky ledge near the shore ($x=95, y=2.5, z=170$).
   - Attach `CoopCarryableItem` with `ItemType.IgnitionCrank`.
   - Add `LighthouseTurnWheel` at the base platform.
   - Attach `LighthouseLightBeam` to the lighthouse lantern point.
2. **East Beacon (Right Island)**:
   - Near the trees ($x=115, y=1.2, z=192$), place the fallen log obstacle with `CoopCrawlVolume`.
   - Underneath, place `Pfb_AncientChest` containing `AncientPrism` (`ItemType.RefractionPrism`).
   - Add `LighthousePrismSocket` on the East Lighthouse circular pedestal.
   - Add `LighthouseTurnWheel` on East Lighthouse to allow rotating the refracted beam.
3. **Giant Watermill**:
   - Add `LuminaMillSensor` component to the watermill arch facing upstream.

---

### 7. Verification & Tests
Ensure all existing Level 2 validations pass:
- Run menu item: `Hold My Hand/Level 2/Wire Beacons Watermill and Victory`
- Verify that `LevelTwoValidation.cs` passes without errors.
- Confirm both host and client replicate beam rotations, item carrying, and sensor activation smoothly.

---

## 🏁 ACCEPTANCE CRITERIA
1. [ ] Con can pick up the `IgnitionCrank` with [E] and hand it over to Ba.
2. [ ] Inserting crank into West Tower powers the primary beam crossing the river.
3. [ ] Ba can lift/brace the fallen log; Con crawls underneath to retrieve the `AncientPrism`.
4. [ ] Con carries `AncientPrism` to East Tower and inserts it into the socket.
5. [ ] Rotating West Tower beam onto East Tower prism creates a refracted secondary beam.
6. [ ] Rotating East Tower prism to target the watermill sensor triggers `BeaconsLit = true`, reduces fog density, and lifts the watermill safety boom.
7. [ ] All network variables replicate properly in Host + Client multiplayer mode.
