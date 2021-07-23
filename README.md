# Gift Wrapping 3D
- Gift Wrapping 3D Algo Visualization in Unity. 
- Created using Psudo Code Reference: https://www.cs.jhu.edu/~misha/Spring16/09.pdf Page 21-24
- Uses Gizmo to draw and visualize output.
- Uses a messy co-routine implementation for the Algo Visualize.

--------------

Note: The outputs is using OnDrawGizmo - so make sure Gizmo is enabled on the unity Editor Scene/Game Window


## TODO
  - Normals
  - Better Step Animation
  - Considering using async task instead of co-routines
 
## Issues with Algo
 - If some points are co-planar, some faces might not be generated
 - Some triangles can be generated inside the convex hull, issue could be linked with

frozonnorth@gmail.com
