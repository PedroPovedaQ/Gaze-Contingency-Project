"""Selection must prefer Wi-Fi without installing on an unintended device."""
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch
from types import SimpleNamespace
import tempfile

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

    def deployment(self, model_ready=True, install=True):
        calls = []
        def run(command, **kwargs):
            calls.append(command)
            if "devices" in command:
                output = "usb device model:VIVE_Focus_Vision\n"
            elif "getprop" in command:
                output = "VIVE Focus Vision"
            elif any(str(x).endswith("local-jev/headset.py") for x in command):
                return SimpleNamespace(returncode=0 if model_ready else 1,
                                       stdout='{"status":"ready"}', stderr="checkpoint missing")
            elif "resolve-activity" in command:
                output = "com.study/app.Main"
            elif "start" in command:
                output = "Status: ok"
            else:
                output = "Success"
            return SimpleNamespace(returncode=0, stdout=output, stderr="")
        with tempfile.TemporaryDirectory() as directory:
            apk = Path(directory) / "study.apk"
            apk.touch()
            args = ["headset-adb.py", "--serial", "usb"]
            if install:
                args += ["--apk", str(apk), "--package", "com.study"]
            with patch.object(adb, "CACHE", Path(directory) / "cache"), \
                 patch.object(adb.sys, "argv", args), \
                 patch.object(adb.subprocess, "run", side_effect=run):
                if model_ready or not install:
                    adb.main()
                else:
                    with self.assertRaisesRegex(RuntimeError, "MiniLM"):
                        adb.main()
        return calls

    def test_update_restores_model_after_install_before_launch_on_same_device(self):
        calls = self.deployment()
        install = next(i for i,c in enumerate(calls) if "install" in c)
        model = next(i for i,c in enumerate(calls) if any(str(x).endswith("local-jev/headset.py") for x in c))
        launch = next(i for i,c in enumerate(calls) if "start" in c)
        self.assertLess(install, model)
        self.assertLess(model, launch)
        self.assertEqual(calls[model][-2:], ["--serial", "usb"])

    def test_failed_model_reconnection_does_not_launch_silent_app(self):
        calls = self.deployment(model_ready=False)
        self.assertFalse(any("start" in c for c in calls))

    def test_device_selection_does_not_start_model_or_change_tunnel(self):
        calls = self.deployment(install=False)
        self.assertFalse(any(any(str(x).endswith("local-jev/headset.py") for x in c) for c in calls))


if __name__ == "__main__":
    unittest.main()
