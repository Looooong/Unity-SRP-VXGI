# Unity - Scriptable Render Pipeline - Voxel-based Global Illumination

## Showcase

- [Tutorial](https://youtu.be/nACG_mtSUDo)
- [Lighting Demo](https://youtu.be/thsw3c0SDIw)
- [The Room Demo](https://youtu.be/cOHHuDeXhgw)
- [Example project (legacy branch)](https://github.com/Looooong/Unity-SRP-VXGI/tree/legacy)

<p align="center">
  <img src="Documentation~/Screenshots/1.jpg" alt="Screenshot 1" width="230" />
  <img src="Documentation~/Screenshots/2.jpg" alt="Screenshot 2" width="300" />
</p>

## Requirements

- Unity 2019.
- Shader Model 4.5 or newer.
- Graphic API that supports geometry shader (this excludes Metal API).
- Approximately 1GB of VRAM for highest voxel resolution setting.

## Installation

[This method](https://docs.unity3d.com/Manual/upm-ui-giturl.html) is the easiest way to install a package. Just add the following dependency to `<project path>/Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.looooong.srp.vxgi": "https://github.com/Looooong/Unity-SRP-VXGI.git"
  }
}
```

If you want to fiddle with the source code while using this package, you can [install the package locally](https://docs.unity3d.com/Manual/upm-ui-local.html).

For more information on how to manage UPM package, please refer to [this](https://docs.unity3d.com/Manual/upm-ui-actions.html).

## Usage

### Enable VXGI Render Pipeline

+ Goto `Asset/Rendering/VXGI Render Pipeline Asset` to create a new VXGI render pipeline asset.
+ Open **Project Settings** window by going to `Edit/Project Settings...`.
+ Assign the newly created render pipeline asset to `Graphics/Scriptable Render Pipeline Settings`.

### Enable VXGI Rendering on a Camera

+ Add `VXGI` component to a Camera.
+ Assign tag `MainCamera` to the same Camera to preview VXGI rendering in Scene View.
+ *Optional:* Add `VXGI Mipmap Debug` to the same Camera to visualize the voxel mipmap volume.

### Apply VXGI materials to objects in the Scene

Apply the material that uses one of the following VXGI shaders:

+ `VXGI/Standard` and `VXGI/Standard (Specular setup)`
  + Only `Albedo`, `Metallic`, `Specular`, `Smoothness`, `Normal Map` and `Emission` are supported.
+ `VXGI/Particles/Standard Unlit`
  + Only `Additive` rendering mode is supported.

### Light Sources

Directional light, point light and spot light are supported. Lighting fall-off follows the inverse-squared distance model without range attenuation. The fall-off model will be expanded with more options in the future.

## Configuration

### VXGI Component Properties

+ **Voxel Volume**:
  + **Follow Camera**: make the voxel volume center follow the camera position.
  + **Center**: the center of the voxel volume in World Space.
  + **Bound**: the size of the voxel volume in World Space.
  + **Resolution**: the resolution of the voxel volume.
  + **Anti Aliasing**: the anti-aliasing level of the voxelization process.
  + **Mipmap Filter Mode**: specify the method to generate the voxel mipmap volume.
  + **Limit Refresh Rate**: limit the voxel volume refresh rate.
  + **Refresh Rate**: the target refresh rate of the voxel volume.
+ **Rendering**:
  + **Indirect Diffuse Modifier**: how strong the diffuse cone tracing can affect the scene.
  + **Indirect Specular Modifier**: how strong the specular cone tracing can affect the scene.
  + **Diffuse Resolution Scale**: downscale the diffuse cone tracing pass.

### VXGI Mipmap Debug Component Properties

+ **Mipmap Level**: Mipmap level to visualize.
+ **Ray Tracing Step**: how big is a step when ray tracing through the voxel volume.
+ **Filter Mode**: visualization filter mode.

## Known Issues and Limitations

+ VXGI uses geometry shader, which is not supported by few graphics APIs, e.g., Metal.
+ The content of Unity UI element is not displayed in Scene View, but only bounding Rect Transform is displayed. Unity UI element is displayed normally in Game View.

## Contributing

All pull requests are welcome.

## License

[MIT](LICENSE.md)

## Backers

Thank you for your support! :pray:

[![David Jeske](https://avatars3.githubusercontent.com/u/15093?s=128)](https://github.com/jeske)

## Acknowledgement

This project is inspired by [sonicether/SEGI](https://github.com/sonicether/SEGI).

Icons made by [Freepik](https://www.flaticon.com/authors/freepik) from [www.flaticon.com](https://www.flaticon.com).
