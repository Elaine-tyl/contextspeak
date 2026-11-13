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
