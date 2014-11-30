<a href="https://www.youtube.com/watch?v=_KpyM0ge1HE" target="_blank">Youtube Video</a>

<a href="http://tangochen.com/blog/?p=1972" target="_blank">Blog Post</a>

Steps to track tip of tongue:
===================

Get the mouth area in the depth image by using face tracking.

Get the smallest depth(then it is closest to the Kinect sensor) inside the mouth area. That's the depth part of the tongue's tip!

Get the relative position according to the mouth area to know the tongue's direction.

Show the tracking results when the mouth is open.


# Public Variables:

**float TongueX;**  tip of tongue's X-axis. Value 0 to 1 represents mouth's left side to right side.

**float TongueY;**  tip of tongue's Y-axis. Value 0 to 1 represents mouth's top to bottom.
