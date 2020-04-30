# Changelog

## [0.0.1]

### Added

- Add `Box` filter to mipmap generator. `resolutionPlusOne` is now replaced with `mipmapFilterMode` in `VXGI` script.
  - If using `Box` filter (faster), voxel resolution will be **2<sup>n</sup>**.
  - If using `Gaussian3x3x3` filter (faster), voxel resolution will be **2<sup>n</sup>+1** (recommended).
  - If using `Gaussian4x4x4` filter (slower), voxel resolution will be **2<sup>n</sup>**.
- Add `resolutionPlusOne` to `VXGI` script.
  - When `true`, voxel resolution will be **2<sup>n</sup>+1**. Mipmap filter will use 3x3x3 Gaussian Kernel (faster).
  - When `false`, voxel resolution will be **2<sup>n</sup>**. Mipmap filter will use 4x4x4 Gaussian Kernel (slower).
- Add angle-based falloff for spot light.
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

- Refactor and optimize mipmap filter to use `groupshared` memory in compute shader. This reduces as much as 40% in filter time.
- Refactor profile sampling.
- Optimize and refactor `VoxelShader`.
- Update README.
- Switch to built-in G-Buffer generator.
- Refactor code structures.

### Deprecated

- Deprecate `Voxel-based Shader/Basic` shader.

### Fixed

- Fix mipmap level visualization.
- Fix light injection when throttling cone tracing.
- Fix light source injection.
- Fix inverted `GrabCopy` pass.
