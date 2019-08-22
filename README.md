# TAA

A implementation of temporal anti-aliasing in Unity.

Included in the Unity project are two scenes, a simple cube, and a fabrication of a city block downloaded from the Unity asset store for further demonstrtive purposes("NYC Block #6" by NONECG).

When startting play mode the camera tends to have a mind of it's own as to where it faces initially, however you can control the view direction with the mouse. 
Additionally, there is a known issue when translating the camera and the motion vectors, resulting in some awful ghosting. 
Unfortunately, as the camera controls were put in late in development, I was not able to correct this issue, though camera rotation still works fine. 

Camera controls:

Mouse = look
W = Forward
S = Backwards
A = Left
D = Right
LShift = Up
LCtrl = Down
Space(Hold) = Locks movement along the X and Z axes

Other options for the scene may be found upon selecting "Main Camera" in the heirarchy menu on the left, and looking at the inspector on the right.

Options available:

Jitter - 
	Jitter Scale - Change the scale of the frustum jitter offset as well as see the what the current offset is.
	
Temporal Reprojection -
	Blend Weight Min/Max - Set the minimum and maximum blend values (how much of the history sample is blended with the new frame)
	Show Velocity - A toggle to render the pixel velocities (This requires you to click back into the game viewport after toggling if in play mode)
	Motion Blur Strength - Set the scaling factor for the motion blur (It's best to keep this value low)