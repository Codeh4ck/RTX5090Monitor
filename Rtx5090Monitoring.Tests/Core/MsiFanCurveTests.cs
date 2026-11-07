using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Control;
using Rtx5090Monitoring.Tests.Infrastructure;

namespace Rtx5090Monitoring.Tests.Core;

internal static class MsiFanCurveTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(ValidatesCurveAndLegacyConfiguration);
        runner.Run(InterpolatesRoundsAndClampsEndpoints);
        runner.Run(HoldsCustomFinalDutyAboveTheLastTemperature);
        runner.Run(DelaysBothDirections);
        runner.Run(HandsLowDutyToAutomaticAndResumesManualControl);
        runner.Run(DelaysAutomaticHandoverInBothDirections);
        runner.Run(PreservesLegacyDelayAndWritesResponseDelay);
        runner.Run(AvoidsRedundantWritesAndVerifiesReadback);
        runner.Run(AutomaticAllowsFirmwareDutyChanges);
        runner.Run(CurveHandoverAllowsNonzeroAutomaticDuty);
        runner.Run(AutomaticRejectsWrongModeAndInvalidDuty);
        runner.Run(LatchesFallbackAcrossReconnectUntilExplicitReset);
        runner.Run(RestoresAutomaticWhenControlIsReleased);
    }

    private static MsiFanCurve Curve(int delay = 5) => new()
    {
        Points = [new(40, 30), new(50, 50), new(75, 100)],
        ResponseDelaySeconds = delay
    };

    private static void ValidatesCurveAndLegacyConfiguration()
    {
        var old = JsonSerializer.Deserialize("""{"fanControl":"Unmanaged","fanDutyPercent":50}""",
            Rtx5090Monitoring.Plugins.Msi.Serialization.MsiJsonContext.Default.MsiOptions)!;
        old.Validate();
        TestAssert.Equal(2, old.FanCurve.ResponseDelaySeconds);
        TestAssert.Equal(MsiFanControl.Unmanaged, old.FanControl);
        var defaults = JsonSerializer.Deserialize("{}",
            Rtx5090Monitoring.Plugins.Msi.Serialization.MsiJsonContext.Default.MsiOptions)!;
        defaults.Validate();
        TestAssert.Equal(50, defaults.FanDutyPercent);

        var curve = Curve();
        curve.Validate();
        TestAssert.Throws<ArgumentException>(() => (curve with { Points = [new(40, 50), new(40, 100)] }).Validate());
        (curve with { Points = [new(35, 50), new(40, 30)] }).Validate();
        TestAssert.Equal(40, MsiFanCurveController.Interpolate([new(35, 50), new(40, 30)], 37.5));
        (curve with { Points = [new(20, 0), new(60, 50)] }).Validate();
        TestAssert.Throws<ArgumentException>(() => (curve with { Points = [new(35.5, 50), new(40, 30)] }).Validate());
        TestAssert.Throws<ArgumentException>(() => (curve with { Points = [new(double.NaN, 50), new(75, 100)] }).Validate());
        TestAssert.Throws<ArgumentException>(() => (curve with { Points = [new(40, 24), new(75, 100)] }).Validate());
        (curve with { Points = [new(40, 50), new(75, 99)] }).Validate();
        TestAssert.Throws<ArgumentException>(() => (curve with
        {
            Points = [.. Enumerable.Range(0, 7).Select(index => new MsiFanCurvePoint(index * 10, 50))]
        }).Validate());
        TestAssert.Throws<ArgumentException>(() => (curve with { ResponseDelaySeconds = -1 }).Validate());
        TestAssert.Throws<ArgumentException>(() => new MsiOptions { FanCurve = null! }.Validate());

        var settings = MsiSettings.WithOptions(new PluginSettings { PollIntervalMs = 6000 },
            new MsiOptions { FanControl = MsiFanControl.Curve, FanCurve = curve });
        TestAssert.Throws<ArgumentException>(() => MsiSettings.GetOptions(settings));
        var valid = MsiSettings.GetOptions(settings with { PollIntervalMs = 1000 });
        TestAssert.True(valid.FanCurve.Matches(curve), "The persisted configuration must retain every curve point.");
    }

    private static void InterpolatesRoundsAndClampsEndpoints()
    {
        var points = Curve().Points;
        TestAssert.Equal(30, MsiFanCurveController.Interpolate(points, 20));
        TestAssert.Equal(40, MsiFanCurveController.Interpolate(points, 45));
        TestAssert.Equal(41, MsiFanCurveController.Interpolate(points, 45.25));
        TestAssert.Equal(50, MsiFanCurveController.Interpolate(points, 50));
        TestAssert.Equal(100, MsiFanCurveController.Interpolate(points, 100));

        var controller = new MsiFanCurveController();
        TestAssert.Throws<IOException>(() => controller.Evaluate(Curve(), double.NaN, DateTimeOffset.UtcNow));
    }

    private static void HoldsCustomFinalDutyAboveTheLastTemperature()
    {
        var curve = new MsiFanCurve { Points = [new(30, 40), new(60, 70)] };
        curve.Validate();
        var controller = new MsiFanCurveController();
        var hardware = new FanHardware();
        var session = new MsiFanControlSession(NullLogger.Instance);
        var options = new MsiOptions { FanControl = MsiFanControl.Curve, FanCurve = curve };
        var now = DateTimeOffset.UtcNow;

        controller.Reset(70);
        session.Update(options, 60, now, hardware.Exchange);
        now = now.AddSeconds(2);

        foreach (var temperature in new[] { 60, 61, 80, 100, 125 })
        {
            TestAssert.Equal(70, controller.Evaluate(curve, temperature, now));
            session.Update(options, temperature, now, hardware.Exchange);
            TestAssert.Equal(70, hardware.Duty);
        }

        TestAssert.Equal(2, hardware.Writes);
    }

    private static void DelaysBothDirections()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = new MsiFanCurveController();
        var curve = Curve(2);
        controller.Reset(50);

        TestAssert.Equal(50, controller.Evaluate(curve, 65, now));
        TestAssert.Equal(50, controller.Evaluate(curve, 70, now.AddSeconds(1)));
        TestAssert.Equal(100, controller.Evaluate(curve, 75, now.AddSeconds(2)));
        TestAssert.Equal(100, controller.Evaluate(curve, 50, now.AddSeconds(3)));
        TestAssert.Equal(100, controller.Evaluate(curve, 49, now.AddSeconds(4)));
        TestAssert.Equal(46, controller.Evaluate(curve, 48, now.AddSeconds(5)));

        // Returning to the applied duty cancels the pending change.
        TestAssert.Equal(46, controller.Evaluate(curve, 45, now.AddSeconds(6)));
        TestAssert.Equal(46, controller.Evaluate(curve, 48, now.AddSeconds(7)));
        TestAssert.Equal(46, controller.Evaluate(curve, 45, now.AddSeconds(8)));
        TestAssert.Equal(46, controller.Evaluate(curve, 45, now.AddSeconds(9)));
        TestAssert.Equal(38, controller.Evaluate(curve, 44, now.AddSeconds(10)));

        controller.Reset();
        TestAssert.Equal(100, controller.Evaluate(Curve(0), 80, now));
        TestAssert.Equal(30, controller.Evaluate(Curve(0), 40, now));
    }

    private static void HandsLowDutyToAutomaticAndResumesManualControl()
    {
        var curve = new MsiFanCurve { Points = [new(20, 0), new(60, 50)], ResponseDelaySeconds = 0 };
        var options = new MsiOptions { FanControl = MsiFanControl.Curve, FanCurve = curve };
        var hardware = new FanHardware();
        var session = new MsiFanControlSession(NullLogger.Instance);
        var now = DateTimeOffset.UtcNow;

        session.Update(options, 39, now, hardware.Exchange);
        TestAssert.Equal(1, hardware.Mode);
        TestAssert.Equal(0, hardware.Duty);
        TestAssert.Equal<int?>(0, session.TargetDuty);
        session.Update(options, 30, now, hardware.Exchange);
        TestAssert.Equal(1, hardware.Writes);
        session.Update(options, 40, now, hardware.Exchange);
        TestAssert.Equal(3, hardware.Mode);
        TestAssert.Equal(25, hardware.Duty);
        session.Update(options, 20, now, hardware.Exchange);
        TestAssert.Equal(1, hardware.Mode);
        TestAssert.Equal(0, hardware.Duty);
        TestAssert.Equal("Curve", session.State(options));

        var manual = new MsiOptions { FanControl = MsiFanControl.Manual, FanDutyPercent = 17 };
        session.Update(manual, 20, now, hardware.Exchange);
        TestAssert.Equal(1, hardware.Mode);
        TestAssert.Equal(0, hardware.Duty);
    }

    private static void DelaysAutomaticHandoverInBothDirections()
    {
        var options = new MsiOptions
        {
            FanControl = MsiFanControl.Curve,
            FanCurve = new MsiFanCurve { Points = [new(20, 0), new(60, 50)] }
        };
        var hardware = new FanHardware { Mode = 3, Duty = 50 };
        var session = new MsiFanControlSession(NullLogger.Instance);
        var now = DateTimeOffset.UtcNow;

        session.Update(options, 39, now, hardware.Exchange);
        TestAssert.Equal(3, hardware.Mode);
        TestAssert.Equal(50, hardware.Duty);
        session.Update(options, 30, now.AddSeconds(1), hardware.Exchange);
        TestAssert.Equal(50, hardware.Duty);
        session.Update(options, 39, now.AddSeconds(2), hardware.Exchange);
        TestAssert.Equal(1, hardware.Mode);
        TestAssert.Equal(0, hardware.Duty);
        session.Update(options, 40, now.AddSeconds(3), hardware.Exchange);
        TestAssert.Equal(1, hardware.Mode);
        session.Update(options, 60, now.AddSeconds(4), hardware.Exchange);
        TestAssert.Equal(0, hardware.Duty);
        session.Update(options, 60, now.AddSeconds(5), hardware.Exchange);
        TestAssert.Equal(3, hardware.Mode);
        TestAssert.Equal(50, hardware.Duty);
    }

    private static void PreservesLegacyDelayAndWritesResponseDelay()
    {
        var options = JsonSerializer.Deserialize(
            """{"fanCurve":{"points":[{"temperatureC":20,"dutyPercent":0},{"temperatureC":60,"dutyPercent":50}],"decreaseDelaySeconds":7}}""",
            Rtx5090Monitoring.Plugins.Msi.Serialization.MsiJsonContext.Default.MsiOptions)!;
        TestAssert.Equal(7, options.FanCurve.ResponseDelaySeconds);
        var json = JsonSerializer.Serialize(options, Rtx5090Monitoring.Plugins.Msi.Serialization.MsiJsonContext.Default.MsiOptions);
        TestAssert.True(json.Contains("\"responseDelaySeconds\":7") && !json.Contains("decreaseDelaySeconds"),
            "Old delay settings must migrate without being lost or written twice.");
    }

    private static void AvoidsRedundantWritesAndVerifiesReadback()
    {
        var hardware = new FanHardware();
        var session = new MsiFanControlSession(NullLogger.Instance);
        var options = new MsiOptions { FanControl = MsiFanControl.Curve, FanCurve = Curve(0) };
        var now = DateTimeOffset.UtcNow;
        session.Update(options, 45, now, hardware.Exchange);
        TestAssert.Equal(40, hardware.Duty);
        TestAssert.Equal(1, hardware.Writes);
        session.Update(options, 45, now.AddSeconds(1), hardware.Exchange);
        TestAssert.Equal(1, hardware.Writes);

        hardware.Duty = 70;
        TestAssert.Throws<IOException>(() => session.Update(options, 60, now.AddSeconds(2), hardware.Exchange));
        TestAssert.Equal(1, hardware.Writes);
    }

    private static void AutomaticAllowsFirmwareDutyChanges()
    {
        var hardware = new FanHardware { AutomaticDuty = 13 };
        var session = new MsiFanControlSession(NullLogger.Instance);
        var options = new MsiOptions { FanControl = MsiFanControl.Automatic };
        var now = DateTimeOffset.UtcNow;

        session.Update(options, 47, now, hardware.Exchange);
        TestAssert.Equal(1, hardware.Mode);
        TestAssert.Equal(13, hardware.Duty);
        TestAssert.Equal<int?>(null, session.TargetDuty);

        hardware.Duty = 35;
        session.Update(options, 55, now.AddSeconds(1), hardware.Exchange);
        TestAssert.Equal(1, hardware.Writes);
        TestAssert.Equal(35, hardware.Duty);

        session.Release(hardware.Exchange);
        TestAssert.Equal(1, hardware.Mode);
        TestAssert.Equal(13, hardware.Duty);
        TestAssert.True(session.Failure is null, "Firmware-managed duty must not trigger a control failure.");
    }

    private static void CurveHandoverAllowsNonzeroAutomaticDuty()
    {
        var hardware = new FanHardware { AutomaticDuty = 13 };
        var session = new MsiFanControlSession(NullLogger.Instance);
        var options = new MsiOptions
        {
            FanControl = MsiFanControl.Curve,
            FanCurve = new MsiFanCurve { Points = [new(20, 0), new(60, 50)], ResponseDelaySeconds = 0 }
        };
        var now = DateTimeOffset.UtcNow;

        session.Update(options, 39, now, hardware.Exchange);
        TestAssert.Equal(1, hardware.Mode);
        TestAssert.Equal(13, hardware.Duty);
        TestAssert.Equal<int?>(0, session.TargetDuty);

        hardware.Duty = 20;
        session.Update(options, 39, now.AddSeconds(1), hardware.Exchange);
        TestAssert.Equal(1, hardware.Writes);

        session.Update(options, 40, now.AddSeconds(2), hardware.Exchange);
        TestAssert.Equal(3, hardware.Mode);
        TestAssert.Equal(25, hardware.Duty);

        session.Fail(new IOException("Lost fresh temperature"), hardware.Exchange);
        TestAssert.Equal(1, hardware.Mode);
        TestAssert.Equal(13, hardware.Duty);
        session.Disconnected();
        session.Update(options, 60, now.AddSeconds(3), hardware.Exchange);
        TestAssert.Equal(1, hardware.Mode);
        TestAssert.Equal("Automatic fallback", session.State(options));
    }

    private static void AutomaticRejectsWrongModeAndInvalidDuty()
    {
        var hardware = new FanHardware { AutomaticDuty = 13 };
        var session = new MsiFanControlSession(NullLogger.Instance);
        var options = new MsiOptions { FanControl = MsiFanControl.Automatic };
        var now = DateTimeOffset.UtcNow;

        session.Update(options, 47, now, hardware.Exchange);
        hardware.Mode = 3;
        TestAssert.Throws<IOException>(() => session.Update(options, 47, now.AddSeconds(1), hardware.Exchange));
        TestAssert.Equal(1, hardware.Writes);

        hardware.Mode = 1;
        hardware.Duty = 101;
        TestAssert.Throws<IOException>(() => session.Update(options, 47, now.AddSeconds(2), hardware.Exchange));
        TestAssert.Equal(1, hardware.Writes);
    }

    private static void LatchesFallbackAcrossReconnectUntilExplicitReset()
    {
        var hardware = new FanHardware();
        var session = new MsiFanControlSession(NullLogger.Instance);
        var options = new MsiOptions { FanControl = MsiFanControl.Curve, FanCurve = Curve(0) };
        var now = DateTimeOffset.UtcNow;
        session.Update(options, 50, now, hardware.Exchange);
        session.Fail(new IOException("Lost fresh temperature"), hardware.Exchange);
        TestAssert.Equal(1, hardware.Mode);
        TestAssert.True(session.Failure is not null && session.TargetDuty is null, "Control failure must be published.");
        session.Disconnected();
        session.Update(options, 60, now.AddSeconds(1), hardware.Exchange);
        TestAssert.Equal(1, hardware.Mode);
        TestAssert.Equal("Automatic fallback", session.State(options));

        session.Release(hardware.Exchange);
        session.ResetFailure();
        session.Update(options, 60, now.AddSeconds(2), hardware.Exchange);
        TestAssert.Equal(3, hardware.Mode);
        TestAssert.Equal(70, hardware.Duty);
        TestAssert.Equal("Curve", session.State(options));
    }

    private static void RestoresAutomaticWhenControlIsReleased()
    {
        var hardware = new FanHardware();
        var session = new MsiFanControlSession(NullLogger.Instance);
        session.Update(new MsiOptions { FanControl = MsiFanControl.Manual, FanDutyPercent = 37 },
            50, DateTimeOffset.UtcNow, hardware.Exchange);
        session.Release(hardware.Exchange);
        TestAssert.Equal(1, hardware.Mode);
        TestAssert.Equal(0, hardware.Duty);
        TestAssert.Equal<int?>(null, session.TargetDuty);
    }

    private sealed class FanHardware
    {
        public int Mode { get; set; } = 1;
        public int Duty { get; set; }
        public int? AutomaticDuty { get; init; }
        public int Writes { get; private set; }

        public byte[] Exchange(byte[] request, int length)
        {
            var reply = new byte[65];

            if (request[1] == 0x50)
            {
                TestAssert.Equal((byte)0x41, request[2]);
                Mode = request[3];
                Duty = Mode == 1 ? AutomaticDuty ?? request[5] : request[5];
                Writes++;
            }
            else
            {
                reply[3] = (byte)(request[2] == 0x41 ? Mode : Duty);
            }

            return reply;
        }
    }
}
