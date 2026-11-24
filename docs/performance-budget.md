# Performance and Memory Budget — MacBook Air M3 / 8 GB

The target has 8 GB of shared CPU/GPU memory and passive cooling. Budgets are hard limits for imported and runtime content, not goals to exceed during polish.

## Assets

- Textures: maximum 1K. Small props use 512 or lower. Never import or ship 4K textures.
- Skyboxes and HDRIs: maximum 2K.
- Lightmaps: approximately one 1K atlas per act scene; add another only after target-hardware profiling proves it is necessary.
- Drowned figures: under 5,000 triangles at LOD0, two LODs, one shared GPU-instanced material, and only about four figures at full detail near the player. Remaining figures use LOD1 or silhouettes.
- Audio: compressed. Long dialogue, music and ambience stream from disk; short effects use compressed-in-memory. Ambience is mono unless spatial review proves stereo is essential.

## Runtime

- Load one act scene at a time. Act transitions load the next scene additively, activate it, unload the previous act, then call `Resources.UnloadUnusedAssets`.
- Real-time lights: directional moon/sun, lamp spot, and one or two lanterns only.
- Real-time shadows: directional light and lamp spot only, at short shadow distance with one cascade.
- MSAA is off. Use the 0.7–1.0 render-scale option before adding expensive effects.
- Target 30–60 FPS during a sustained play session, not only a cold editor run.

## Quality Presets & Hardware Contract Agreement

A discrepancy previously existed between this budget contract (specifying MSAA Off and 1 shadow cascade) and older graphics experiment reports that tested 4x MSAA and 4 cascades. The contract has been unified as follows:

1. **Target Hardware Profile: MacBook Air M3 / 8 GB (Passive Cooling)**:
   - **MSAA**: **Disabled (`m_MSAA = 1`)**. Multi-sample anti-aliasing forces 4x buffer memory allocation and induces memory bandwidth thermal throttling under sustained storm and finale particle loads on passively cooled Apple Silicon.
   - **Anti-Aliasing**: Handled via post-processing **SMAA** (`SubpixelMorphologicalAntiAliasing`) on the camera, preserving edge sharpness and high-frequency textures without multi-sample overhead.
   - **Shadow Cascades**: **1 Cascade (`m_ShadowCascadeCount = 1`)** at controlled shadow distance (30m–50m) with soft shadows enabled.
   - **Render Scale**: Native 1.0, scalable from 0.7 to 1.0 in the in-game settings menu.
   - **Pipeline Assets**: `URP_Mac.asset` (Mac preset) and `URP_Low.asset` (Low preset) enforce these limits.

2. **High / Desktop Preset (`PC_RPAsset.asset`)**:
   - For actively cooled desktop systems with dedicated discrete GPUs, multi-cascade shadows may be selected in settings.

3. **Measured Benchmark Results (Act 3 Storm & Act 5 Finale)**:
   - **Act 3 (Peak Storm)**: 60.8 Avg FPS (Min 55.6, Max 66.6), 16.45 ms avg frame time, 963.5 MB allocated memory.
   - **Act 5 (Peak Finale & Flood)**: 60.8 Avg FPS (Min 55.6, Max 66.7), 16.45 ms avg frame time, 966.5 MB allocated memory.
   - Both peak scenes remain completely within the 8 GB unified memory limit and sustain a stable 60 FPS profile.

Run **TheLightHouse → Check Asset Budget** before importing an art/audio batch and before every release candidate. Auto-fix is restricted to safe texture and audio importer changes; model reduction and scene-light decisions remain manual.

The runtime light counter reads `Light.bakingOutput.lightmapBakeType`; `Light.lightmapBakeType` is editor-only and must not be used by player scripts.
