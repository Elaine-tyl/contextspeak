import bpy, sys, os
from mathutils import Vector
out = sys.argv[sys.argv.index("--")+1]
def prep(path, name, ratio, height, pivot):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    o=[x for x in bpy.data.objects if x.type=='MESH'][0]
    bpy.context.view_layer.objects.active=o; o.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if ratio < 1:
        m=o.modifiers.new("d","DECIMATE"); m.ratio=ratio; bpy.ops.object.modifier_apply(modifier="d")
    ws=[o.matrix_world@v.co for v in o.data.vertices]
    mn=Vector((min(v.x for v in ws),min(v.y for v in ws),min(v.z for v in ws))); mx=Vector((max(v.x for v in ws),max(v.y for v in ws),max(v.z for v in ws)))
    s=height/(mx.z-mn.z)
    # pivot: bottom centre of the shaft/handle (x,y = vertex column nearest the vertical axis at the bottom)
    bottom=[v for v in ws if v.z < mn.z + (mx.z-mn.z)*0.04]
    px=sum(v.x for v in bottom)/len(bottom); py=sum(v.y for v in bottom)/len(bottom)
    if pivot=="axis": px=(mn.x+mx.x)/2; py=(mn.y+mx.y)/2
    for v in o.data.vertices: v.co=(v.co-Vector((px,py,mn.z)))*s
    o.name=name; o.data.name=name
    tris=sum(len(p.vertices)-2 for p in o.data.polygons)
    bpy.ops.export_scene.gltf(filepath=os.path.join(out,name+".glb"), export_format="GLB", export_yup=True)
    print("PROP",name,"tris",tris,"size",tuple(round((mx[i]-mn[i])*s,2) for i in range(3)))
prep("/tmp/new/rs1/Meshy_AI_Rainshadow_20260930_texture_fbx/Meshy_AI_Rainshadow_20260930_texture.fbx","RainUmbrella",0.03,0.95,"axis")
prep("/tmp/new/umb/Meshy_AI_Pastel_Beach_Umbrella_20260702_texture_remesh_tri_fbx/Meshy_AI_Pastel_Beach_Umbrella_20260702_texture_remesh_tri.fbx","PastelParasol",1.0,2.3,"axis")
