# Unity - Scriptable Render Pipeline - Voxel-based Global Illumination

![Screenshot 1](Documentation~/Screenshots/1.jpg)
![Screenshot 2](Documentation~/Screenshots/2.jpg)

# Installation
Add the following dependency to `<project path>/Packages/manifest.json`:

```json
{
  "dependencies": {
    // ...
    "com.looooong.srp.vxgi": "https://github.com/Looooong/Unity-SRP-VXGI.git"
  }
}
```

# Requirements
+ Unity 2018 (Experimental API). Using Unity 2019 would require slight modification to the code.
+ Shader Model 4.5 or newer.
+ Graphic API that supports geometry shader (this excludes Metal API).
+ Approximately 1GB of VRAM for highest voxel resolution setting.

# Acknowledgement
This project is inspired by [sonicether/SEGI](https://github.com/sonicether/SEGI).
