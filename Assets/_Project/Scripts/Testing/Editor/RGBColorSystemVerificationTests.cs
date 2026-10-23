using System.Text;
using UnityEditor;
using UnityEngine;
using Recorded.ColorSystem;

namespace Recorded.Testing.Editor
{
    /// <summary>
    /// Automated verification suite for the RECORDED RGB object color system.
    /// Validates all 11 test cases specified in the task requirements.
    /// Can be run via the Unity Editor menu: RECORDED > Tests > Run RGB Color System Verification.
    /// </summary>
    public static class RGBColorSystemVerificationTests
    {
        [MenuItem("RECORDED/Tests/Run RGB Color System Verification", false, 10)]
        public static void RunAllTests()
        {
            var results = new StringBuilder();
            results.AppendLine("<b>=== RECORDED RGB Color System Verification (11 Cases) ===</b>");
            int passedCount = 0;
            const int totalCount = 11;

            GameObject testObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testObj.name = "Test_RGBTarget_Temp";
            RGBColorTarget target = testObj.AddComponent<RGBColorTarget>();

            try
            {
                // Test 1: Fresh target starts grey (#7F7F7F)
                bool pass1 = (target.CurrentColorState == ObjectColorState.None) &&
                             (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorNeutralGrey);
                LogCase(1, "Fresh target starts grey (#7F7F7F)", pass1, ref passedCount, results);

                // Test 2: Red changes it to red
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Red);
                bool pass2 = (target.CurrentColorState == ObjectColorState.Red) &&
                             (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorRed);
                LogCase(2, "Red changes it to red", pass2, ref passedCount, results);

                // Test 3: Red + Green changes it to yellow
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Red);
                target.ApplyColor(ObjectColorState.Green);
                bool pass3 = (target.CurrentColorState == (ObjectColorState.Red | ObjectColorState.Green)) &&
                             (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorYellow);
                LogCase(3, "Red + Green changes it to yellow", pass3, ref passedCount, results);

                // Test 4: Red + Blue changes it to magenta
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Red);
                target.ApplyColor(ObjectColorState.Blue);
                bool pass4 = (target.CurrentColorState == (ObjectColorState.Red | ObjectColorState.Blue)) &&
                             (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorMagenta);
                LogCase(4, "Red + Blue changes it to magenta", pass4, ref passedCount, results);

                // Test 5: Green + Blue changes it to cyan
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Green);
                target.ApplyColor(ObjectColorState.Blue);
                bool pass5 = (target.CurrentColorState == (ObjectColorState.Green | ObjectColorState.Blue)) &&
                             (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorCyan);
                LogCase(5, "Green + Blue changes it to cyan", pass5, ref passedCount, results);

                // Test 6: Red + Green + Blue changes it to white
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Red);
                target.ApplyColor(ObjectColorState.Green);
                target.ApplyColor(ObjectColorState.Blue);
                bool pass6 = (target.CurrentColorState == (ObjectColorState.Red | ObjectColorState.Green | ObjectColorState.Blue)) &&
                             (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorWhite);
                LogCase(6, "Red + Green + Blue changes it to white", pass6, ref passedCount, results);

                // Test 7: Duplicate hit does not reset or extend timer
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Red);
                float originalExpire = target.GetRawExpireTime(ObjectColorState.Red);
                // Duplicate hit while Red is still active
                target.ApplyColor(ObjectColorState.Red);
                float afterDuplicateExpire = target.GetRawExpireTime(ObjectColorState.Red);
                bool pass7 = Mathf.Approximately(originalExpire, afterDuplicateExpire);
                LogCase(7, "Applying active color twice preserves original expiration (does not reset/extend)", pass7, ref passedCount, results);

                // Test 8: Second color starts separate timer without changing first color's expiration
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Red);
                float redExpireBefore = target.GetRawExpireTime(ObjectColorState.Red);
                target.ApplyColor(ObjectColorState.Green);
                float redExpireAfter = target.GetRawExpireTime(ObjectColorState.Red);
                float greenExpire = target.GetRawExpireTime(ObjectColorState.Green);
                bool pass8 = Mathf.Approximately(redExpireBefore, redExpireAfter) && greenExpire > 0f;
                LogCase(8, "Applying second color starts separate timer without changing first color's expiration", pass8, ref passedCount, results);

                // Test 9: When one color expires, remaining color combination is displayed correctly
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Red);
                target.ApplyColor(ObjectColorState.Green);
                // Set Red to expire at 205, Green to expire at 207
                target.SetExpirationsForTesting(205f, 207f, -1f);
                // Advance time to 206 (Red expired, Green still active)
                target.SimulateExpirationCheck(206f);
                bool pass9 = (target.CurrentColorState == ObjectColorState.Green) &&
                             (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorGreen);
                LogCase(9, "When one color expires, remaining color (Green) is displayed correctly", pass9, ref passedCount, results);

                // Test 10: When all colors expire, target returns to grey
                // Advance time to 208 (Green expired too)
                target.SimulateExpirationCheck(208f);
                bool pass10 = (target.CurrentColorState == ObjectColorState.None) &&
                              (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorNeutralGrey);
                LogCase(10, "When all colors expire, target returns to neutral grey", pass10, ref passedCount, results);

                // Test 11: Applying Red again after its expiration works normally
                target.ApplyColor(ObjectColorState.Red);
                bool pass11 = (target.CurrentColorState == ObjectColorState.Red) &&
                              (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorRed) &&
                              (target.GetRawExpireTime(ObjectColorState.Red) > 0f);
                LogCase(11, "Applying Red again after its expiration works normally", pass11, ref passedCount, results);
            }
            finally
            {
                Object.DestroyImmediate(testObj);
            }

            results.AppendLine($"\n<b>Total Passed: {passedCount} / {totalCount}</b>");
            if (passedCount == totalCount)
            {
                Debug.Log($"<color=#00FF88>{results}</color>");
            }
            else
            {
                Debug.LogError(results.ToString());
            }
        }

        private static void LogCase(int num, string desc, bool pass, ref int passedCount, StringBuilder sb)
        {
            if (pass)
            {
                passedCount++;
                sb.AppendLine($"[PASS] Case {num}: {desc}");
            }
            else
            {
                sb.AppendLine($"[FAIL] Case {num}: {desc}");
            }
        }
    }
}
