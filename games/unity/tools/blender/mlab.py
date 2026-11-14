"""Quadric edge-collapse with texture preservation (MeshLab). Usage: mlab.py in.obj out.obj target_faces"""
import sys, pymeshlab
src, dst, target = sys.argv[1], sys.argv[2], int(sys.argv[3])
plain = len(sys.argv) > 4 and sys.argv[4] == 'plain'   # ignore UV seams (texture is re-baked afterwards)
ms = pymeshlab.MeshSet()
ms.load_new_mesh(src)
m = ms.current_mesh()
print('in', m.face_number())
if m.face_number() > target:
    for _ in range(12):   # big reductions converge better in a few steps
        cur = ms.current_mesh().face_number()
        if cur <= target * 1.05: break
        step = max(target, int(cur * 0.25))
        if plain:
            ms.meshing_decimation_quadric_edge_collapse(targetfacenum=step, qualitythr=0.1, preserveboundary=False, preservenormal=False, preservetopology=False, planarquadric=True, optimalplacement=True, autoclean=True)
        else:
            ms.meshing_decimation_quadric_edge_collapse_with_texture(targetfacenum=step, qualitythr=0.05, preserveboundary=False, preservenormal=False, planarquadric=True, optimalplacement=True)
print('out', ms.current_mesh().face_number())
ms.save_current_mesh(dst, save_vertex_color=False, save_vertex_normal=True, save_wedge_texcoord=not plain, save_textures=False)
