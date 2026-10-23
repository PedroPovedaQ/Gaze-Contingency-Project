"""Selection must prefer Wi-Fi without installing on an unintended device."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("headset_adb", Path(__file__).parents[1] / "headset-adb.py")
adb = importlib.util.module_from_spec(spec)
spec.loader.exec_module(adb)


class HeadsetSelectionTests(unittest.TestCase):
    def test_wifi_preferred_over_usb_and_phone(self):
        rows = adb.parse_devices("List of devices attached\nusb device model:VIVE_Focus_Vision\n"
                                 "192.168.1.208:5555 device model:VIVE_Focus_Vision\nphone device model:Pixel\n")
        self.assertEqual(adb.choose_device(rows), "192.168.1.208:5555")

    def test_single_usb_fallback(self):
        self.assertEqual(adb.choose_device([("usb", "device", "model:VIVE_Focus_Vision")]), "usb")

    def test_offline_unauthorized_and_other_models_rejected(self):
        for row in [("wifi:5555", "offline", "model:VIVE_Focus_Vision"),
                    ("usb", "unauthorized", "model:VIVE_Focus_Vision"),
                    ("phone", "device", "model:Pixel")]:
            with self.subTest(row=row), self.assertRaises(RuntimeError):
                adb.choose_device([row])

    def test_ambiguous_headsets_rejected(self):
        with self.assertRaises(RuntimeError):
            adb.choose_device([("one:5555", "device", "model:VIVE_Focus_Vision"),
                               ("two:5555", "device", "model:VIVE_Focus_Vision")])

    def test_tls_wireless_preferred(self):
        self.assertEqual(adb.choose_device([
            ("usb", "device", "model:VIVE_Focus_Vision"),
            ("adb-vive._adb-tls-connect._tcp", "device", "model:VIVE_Focus_Vision")]),
            "adb-vive._adb-tls-connect._tcp")


if __name__ == "__main__":
    unittest.main()
