"""Prepared poses may relax outstretched arms without changing the source FBX."""
import unittest
from tools.meshy.pose import relaxed_arm


class PoseTests(unittest.TestCase):
    def test_t_pose_hands_lower_symmetrically(self):
        left = relaxed_arm((.6, 0, .78), 1)
        right = relaxed_arm((-.6, 0, .78), 1)
        self.assertLess(left[0], .4)
        self.assertLess(left[2], .45)
        self.assertAlmostEqual(left[0], -right[0])
        self.assertAlmostEqual(left[2], right[2])

    def test_torso_head_and_skirt_are_unchanged(self):
        for point in [(0, 0, .78), (.1, 0, .7), (.2, 0, .96), (.3, 0, .5)]:
            self.assertEqual(relaxed_arm(point, 1), point)
