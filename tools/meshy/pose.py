"""Small pose adjustment for explicitly inspected, unrigged T-pose copies."""
from math import cos, radians, sin


def relaxed_arm(point, height):
    """Rotate outstretched arms down; retain torso, head, skirt and source files."""
    x, y, z = point
    if not .62 * height < z < .9 * height or abs(x) <= .13 * height:
        return tuple(point)
    weight = min(1, (abs(x) / height - .13) / .12)
    weight = weight * weight * (3 - 2 * weight)
    angle = radians(65) * weight
    dx, dz = abs(x) - .13 * height, z - .78 * height
    side = 1 if x >= 0 else -1
    return (side * (.13 * height + dx * cos(angle) + dz * sin(angle)), y,
            .78 * height - dx * sin(angle) + dz * cos(angle))


def pose_arms(meshes, height, drop=72.0, swing=8.0, elbow=14.0):
    """Lower outstretched (T/A-pose) arms with a two-bone skin instead of bending vertices.

    Each arm is found from the geometry (a centre line through the arm vertices), split into an
    upper arm, a forearm and a rigid hand, then rotated about the shoulder (down to `drop`
    degrees below horizontal, `swing` degrees forward) with a small elbow bend. Weights blend
    only at the shoulder and the elbow, so wrists, hands and fingers keep their shape.
    Coordinates are metres after the converter's floor/height normalisation; the model faces −Y.
    """
    import numpy as np
    from mathutils import Matrix, Vector

    h = height
    data = []
    for obj in meshes:
        world = np.array(obj.matrix_world)
        co = np.empty(len(obj.data.vertices) * 3)
        obj.data.vertices.foreach_get('co', co)
        co = co.reshape(-1, 3)
        points = co @ world[:3, :3].T + world[:3, 3]
        data.append((obj, world, points))
    allpoints = np.concatenate([p for _, _, p in data])

    def smooth(a, b, x):
        t = np.clip((x - a) / (b - a), 0, 1)
        return t * t * (3 - 2 * t)

    def rotation(axis, degrees):
        return np.array(Matrix.Rotation(np.radians(degrees), 3, Vector(axis)))

    for side in (1, -1):
        sx = allpoints[:, 0] * side
        band = (sx > 0.17 * h) & (allpoints[:, 2] > 0.45 * h) & (allpoints[:, 2] < 0.95 * h)
        arm = allpoints[band]
        if len(arm) < 50:
            continue
        reach = (arm[:, 0] * side).max()
        if reach < 0.3 * h:
            continue          # arms already down
        # Centre line of the arm, binned outward from the shoulder.
        edges = np.linspace(0.17 * h, reach, 12)
        line = []
        for a, b in zip(edges[:-1], edges[1:]):
            chunk = arm[((arm[:, 0] * side) >= a) & ((arm[:, 0] * side) < b)]
            if len(chunk):
                line.append(np.median(chunk, axis=0))
        line = np.array(line)
        first = line[0]
        pivot = np.array([side * 0.115 * h, first[1], first[2] + 0.01 * h])
        tip = line[-1]
        length = np.linalg.norm(tip - pivot)
        direction = (tip - pivot) / length
        elbow_point = pivot + direction * length * 0.46
        # Rotation of the whole arm about the shoulder: from the current direction to "down".
        current = np.degrees(np.arctan2(direction[2], abs(direction[0])))        # elevation above horizontal
        lower = rotation((0, 1, 0), side * (current + drop))                     # about the front axis
        forward = rotation((0, 0, 1), -side * swing)                              # hands a little in front of the hips
        upper = forward @ lower
        bend = rotation((0, 0, 1), -side * elbow)                                   # forearm swings forward (−Y)
        new_elbow = pivot + upper @ (elbow_point - pivot)

        for obj, world, points in data:
            rel = points - pivot
            t = rel @ direction / length                                             # 0 shoulder … 1 fingertips
            radial = np.linalg.norm(rel - np.outer(t * length, direction), axis=1)
            thick = 0.075 * h * (1.0 + 0.4 * np.clip(1 - t, 0, 1))
            member = (points[:, 0] * side > 0.09 * h) & (t > -0.05) & (t < 1.15) & (radial < thick)
            w_arm = smooth(0.105 * h, 0.175 * h, points[:, 0] * side) * member
            w_fore = smooth(0.40, 0.52, t)
            moved_upper = (rel @ upper.T) + pivot
            moved_fore = ((points - elbow_point) @ (upper @ bend).T) + new_elbow
            target = moved_upper * (1 - w_fore)[:, None] + moved_fore * w_fore[:, None]
            points[:] = points * (1 - w_arm)[:, None] + target * w_arm[:, None]

    for obj, world, points in data:
        inverse = np.linalg.inv(world)
        local = points @ inverse[:3, :3].T + inverse[:3, 3]
        obj.data.vertices.foreach_set('co', local.ravel())
        obj.data.update()
