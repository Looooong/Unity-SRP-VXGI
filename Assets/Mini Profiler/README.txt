===============================================================================
KLEBER.SWF MINI PROFILER STANDARD README FILE
===============================================================================

Thank you for using Mini Profiler tool. A simple tool that will help you in
your journey to create a great game. With this tool you can watch framerate
and memory usage following their values over time.

You can get more help clicking on the "?" icon on any Mini Profiler component
inside Unity Editor inspector (Unity 5.1+) or going to complete documentation
at: http://kleber-swf.com/docs/mini-profiler/

-------------------------------------------------------------------------------
PACKAGE CONTENT
-------------------------------------------------------------------------------
* Base behaviour scripts
* 1 bitmap font
* Example scene to view the Mini Profiler in action at no time
* 2 Prefabs containing ready to use Framerate and Memory watchers to help you
  to start right away

-------------------------------------------------------------------------------
FEATURES
-------------------------------------------------------------------------------
* Watch framerate and memory usage. The package contains the FPS (framerate)
and memory watcher prefabs for you. You can start to watch these variables in
no time.

* Add panels even easily with a menu. Even more useful then prefabs, right
click on the scene or go to Game Object > UI > Mini Profiler and add a ready
to use Framerate and Memory Watcher panels.

* Minimum impact. The performance impact of the panels is minimal: only 2 draw
calls and a small 256x64 texture into the memory for each panel. Of course the
final performance depends on what is being watched and how often the variable is
being read. So keep this in mind when you create your custom Value Provider.

* Works on any device. PC, OSX, Android, iOS, Web Player, WebGL and all the
platforms Unity can build.

* Customize the interval which the variable is read. Some variables need to be
watched every frame, some every second, some every minute. Mini Profiler let you
configure how often the variable.

* Position, scale and rotate as you wish. The panel is covering some important
part? Positionate it in another place. The panel is too small or too large?
Scale it as you wish. If none helps, you can set the graphic transparency too.

* Pixel font. This will help you to make the text readable when profiler needs
to be very small.

* Create/destroy, enable/disable panels programmatically. You can instantiate,
destroy, enable, disable, scale, position panels as you wish programmatically
throught the MiniProfiler classes

-------------------------------------------------------------------------------
WATCHING THE FRAMERATE AND MEMORY USAGE
-------------------------------------------------------------------------------
To watch the framerate (fps) and the memory usage, you can just grab one of the
Prefabs that come with the package inside "Mini Profiler/Prefabs" folder.

-------------------------------------------------------------------------------
CREATING PANELS PROGRAMATICALLY
-------------------------------------------------------------------------------
You can either instantiate one of the given prefabs or you can create a new
panel like this:

	GameObject go = new GameObject("Framerate Watcher");
	go.AddComponent<FramerateValueProvider>();
	go.AddComponent<MiniProfiler>();

Note that the order is important since the MiniProfiler behaviour depends on a
value provider to work properly.

-------------------------------------------------------------------------------
FURTHER WORDS
-------------------------------------------------------------------------------
The intention of this extension is to watch framerate and memory usage. I use
it daily in my professional and personal projects. It was created trying be
simple, flexible, easy to use and have as less impact as possible in the game.

More information: http://kleber-swf.com/app/mini-profiler/
Complete documentation: http://kleber-swf.com/docs/mini-profiler/
Bugs and requests: https://bitbucket.org/kleber/mini-profiler/issues

If you need more features like watch any numeric varible, color schemes,
dynamically ajust the position of panels, minimization and keyboard shortcuts
checkout the Mini Profiler PRO version at:

	https://www.assetstore.unity3d.com/#!/content/65997

THANK YOU FOR USING MINI PROFILE!
