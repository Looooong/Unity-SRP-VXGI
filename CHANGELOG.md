# Changelog

## [0.0.1]

### Added

- Add `VXGI/Particles/Standard Unlit` shader.
- Add CHANGELOG.
- Complete the light injections.
  - Add support for spot light.
  - Fully support directional light.
  - The `sun` property is removed from VXGI script, as the number of directional lights in a scene is not limited to one anymore.
  - Lighting range is now affected by the `range` property on light source.
- Add support for post-processing stack.
- Add a default material and a default shader to the render pipeline.
- Implement nearest-depth filtering for copying textures of different dimensions.
- Add setting for Dynamic Batching.
- Add setting for Scriptable Render Pipeline Batching.
- Initial commit.

### Changed

- Optimize and refactor `VoxelShader`.
- Update README.
- Switch to built-in G-Buffer generator.
- Refactor code structures.

### Deprecated

- Deprecate `Voxel-based Shader/Basic` shader.

### Fixed

- Fix light source injection.
- Fix inverted `GrabCopy` pass.
