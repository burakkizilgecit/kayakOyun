using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace SledSurfers.EditorTools
{
    /// Bounded measurements of the actual snow generator and runtime physics. No gameplay tuning lives here.
    public static class SnowTerrainTests
    {
        const float Dt = 1f / 240f;
        const float MaxSeconds = 180f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [Serializable]
        public sealed class Flight
        {
            public float startZ, endZ, seconds, distance, startSpeed, endSpeed;
            public float startClearance, endClearance, maxClearance, landingImpact;
            public bool landed, ravine, meaningful;
            public string endCause;
        }

        [Serializable]
        public sealed class Run
        {
            public string mode, player, end, cause;
            public int level, rocketLevel, seed, landings, meaningfulFlights, rocketsFired;
            public float seconds, distance, fill, launchSpeed, topSpeed, maxImpact, maxClearance;
            public float firstMeaningfulZ = -1f, longestAirSeconds, longestAirDistance;
            public float airTime, airDistance, timeBefore300, airTimeBefore300, airShareBefore300;
            public float timeBefore600, airTimeBefore600, airShareBefore600;
            public float airDistanceBefore300, airDistanceBefore600;
            public float wingArea, glideTime, friction, crashImpact;
            public List<Flight> flights = new List<Flight>();
        }

        [Serializable]
        public sealed class Report
        {
            public string label, generatedUtc, note;
            public int track = 3, parts = 3;
            public float step = Dt, maxSeconds = MaxSeconds, length;
            public bool useGlide;
            public float waveAmplitude, waveLength, slowGrade, slowGradeEnd, cycleScale;
            public List<Run> runs = new List<Run>();
        }

        public static void Measure()
        {
            string label = "measure";
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "out"));
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("snowLabel=", StringComparison.Ordinal)) label = arg.Substring(10);
                if (arg.StartsWith("snowOut=", StringComparison.Ordinal)) output = Path.GetFullPath(arg.Substring(8));
            }
            if (string.IsNullOrEmpty(label)) throw new ArgumentException("snowLabel must not be empty");
            foreach (char c in label)
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_') throw new ArgumentException("snowLabel accepts letters, digits, '-' and '_' only");
            int oldTrack = Upgrades.Track, oldParts = Upgrades.PartTier;
            try
            {
                Upgrades.PartTier = 3;
                var track = TrackLibrary.Get(3);
                var report = new Report
                {
                    label = label, generatedUtc = DateTime.UtcNow.ToString("O", Inv), length = track.finishZ,
                    useGlide = TrackLibrary.UseGlide, waveAmplitude = TrackLibrary.WaveAmp[3], waveLength = TrackLibrary.WaveLength[3],
                    slowGrade = TrackLibrary.SlowGrade[3], slowGradeEnd = TrackLibrary.SlowGradeEnd[3], cycleScale = TrackLibrary.CycleScale[3],
                    note = "Actual snow, Parts=3, no avatar perk, full pull with SimPlayer perfect-launch policy. Meaningful flight: excludes launch and ravine falls, >0.4s and >8m. Air shares include all non-launch airborne motion while below the distance cutoff. Pure terrain never fires rockets, including pickups. Progression uses ordinary SimPlayer rocket policy."
                };
                foreach (int level in new[] { 0, 5, 10, 20, 30 })
                    foreach (PlayerKind kind in new[] { PlayerKind.Usta, PlayerKind.Ortalama })
                        foreach (int seed in new[] { 97, 194, 291 })
                            report.runs.Add(Simulate(track, level, 0, kind, seed, false));
                foreach (int level in new[] { 5, 15, 30 })
                    foreach (PlayerKind kind in new[] { PlayerKind.Usta, PlayerKind.Ortalama })
                        report.runs.Add(Simulate(track, level, level, kind, 97, true));

                Directory.CreateDirectory(output);
                string prefix = Path.Combine(output, "snow-terrain-" + label);
                File.WriteAllText(prefix + ".json", JsonUtility.ToJson(report, true), Encoding.UTF8);
                File.WriteAllText(prefix + ".csv", RunsCsv(report), Encoding.UTF8);
                File.WriteAllText(prefix + "-flights.csv", FlightsCsv(report), Encoding.UTF8);
                File.WriteAllText(prefix + "-profile.csv", ProfileCsv(track), Encoding.UTF8);
                var log = new StringBuilder("[SIM] Snow terrain " + label + " | " + report.runs.Count + " runs | " + F(track.finishZ) + " real metres\n");
                log.AppendLine(report.note);
                foreach (Run run in report.runs)
                    log.AppendLine(string.Format(Inv,
                        "  {0} lv{1} rocket{2} {3} seed{4}: {5:F1}m, firstFlight={6:F1}m, flights={7}, longest={8:F2}s/{9:F1}m, air300={10:P0}, air600={11:P0}, speed={12:F1}m/s, impact={13:F2}m/s, fill={14:P0}, {15}/{16}",
                        run.mode, run.level, run.rocketLevel, run.player, run.seed, run.distance, run.firstMeaningfulZ,
                        run.meaningfulFlights, run.longestAirSeconds, run.longestAirDistance, run.airShareBefore300,
                        run.airShareBefore600, run.topSpeed, run.maxImpact, run.fill, run.end, run.cause));
                log.AppendLine("Files: " + prefix + ".json/.csv/.log, -flights.csv, -profile.csv");
                File.WriteAllText(prefix + ".log", log.ToString(), Encoding.UTF8);
                Debug.Log(log.ToString());
            }
            finally
            {
                Upgrades.Track = oldTrack;
                Upgrades.PartTier = oldParts;
            }
        }

        // One editor session for the numerical report, existing core contracts and the real-player inspection build.
        public static void MeasureAndBuildWindows()
        {
            Measure();
            SimTests.SmokeRegression();
            BuildTools.BuildWindowsTest();
        }

        static Run Simulate(TrackProfile track, int level, int rocketLevel, PlayerKind kind, int seed, bool useRocket)
        {
            var cfg = Upgrades.BuildConfig(new[] { level, level, level, 0, rocketLevel });
            var run = new Run
            {
                mode = useRocket ? "progression" : "terrain", player = SimPlayer.Name(kind), level = level,
                rocketLevel = rocketLevel, seed = seed, wingArea = cfg.wingArea, glideTime = cfg.glideTime,
                friction = cfg.groundFriction, crashImpact = cfg.crashImpact
            };
            var sled = new SledPhysics(track, cfg);
            var liquid = new LiquidSim();
            var player = new SimPlayer(kind, seed);
            liquid.Reset(cfg);
            sled.PlaceForAim(1f);
            sled.Launch();
            if (player.PerfectLaunch()) { sled.launchBoost = SledPhysics.PerfectLaunchBoost; sled.stats.perfectLaunch = true; }
            Flight flight = null;
            float time = 0f;
            while (sled.end == RunEnd.None && time < MaxSeconds)
            {
                bool grounded = sled.grounded, launching = sled.launching;
                float z0 = sled.position.z;
                int landings = sled.landings;
                Vector2 glass = player.Glass(sled, cfg, Dt);
                if (useRocket && player.Fire(track, sled, cfg)) sled.FireRocket();
                sled.Step(player.Steer(track, sled, Dt), player.Pitch(track, sled, cfg), Dt);
                liquid.lidClosed = sled.position.z < cfg.lidDistance;
                float fillBefore = liquid.FillRatio;
                liquid.Step(sled.acceleration, SimPlayer.GlassRotation(sled, glass), Dt);
                float lost = fillBefore - liquid.FillRatio;
                if (lost > 0f) sled.stats.milkLost[(int)RunStats.Classify(sled, track)] += lost;
                if (liquid.FillRatio < 0.12f) sled.ForceEnd(RunEnd.Spilled, "Bardak bosaldi!");
                float clearance = sled.position.y - track.Height(sled.position.x, sled.position.z);
                run.maxClearance = Mathf.Max(run.maxClearance, clearance);
                float dz = Mathf.Max(0f, sled.position.z - z0);
                bool airborne = !launching && (!grounded || !sled.grounded);
                if (airborne) { run.airTime += Dt; run.airDistance += dz; }
                float share300 = FractionBelow(z0, sled.position.z, 300f);
                float share600 = FractionBelow(z0, sled.position.z, 600f);
                run.timeBefore300 += Dt * share300;
                run.timeBefore600 += Dt * share600;
                if (airborne)
                {
                    run.airTimeBefore300 += Dt * share300;
                    run.airTimeBefore600 += Dt * share600;
                    run.airDistanceBefore300 += dz * share300;
                    run.airDistanceBefore600 += dz * share600;
                }
                if (grounded && !sled.grounded && !launching && !sled.launching)
                    flight = new Flight
                    {
                        startZ = sled.position.z, startSpeed = sled.Speed, startClearance = clearance,
                        maxClearance = clearance, ravine = track.OverRavine(sled.position.x, sled.position.z)
                    };
                if (flight != null)
                {
                    flight.seconds += Dt;
                    flight.maxClearance = Mathf.Max(flight.maxClearance, clearance);
                }
                if (sled.landings != landings)
                {
                    run.maxImpact = Mathf.Max(run.maxImpact, sled.lastImpact);
                    if (flight != null) FinishFlight(run, flight, sled, clearance, true);
                    flight = null;
                }
                time += Dt;
            }
            if (flight != null) FinishFlight(run, flight, sled, sled.position.y - track.Height(sled.position.x, sled.position.z), false);
            run.seconds = time;
            run.distance = sled.maxDistance;
            run.fill = liquid.FillRatio;
            run.launchSpeed = sled.stats.launchSpeed;
            run.topSpeed = sled.topSpeed;
            run.landings = sled.landings;
            run.rocketsFired = sled.stats.rocketsFired;
            run.end = sled.end == RunEnd.None ? "Timeout" : sled.end.ToString();
            run.cause = sled.crashCause.ToString();
            run.airShareBefore300 = run.airTimeBefore300 / Mathf.Max(Dt, run.timeBefore300);
            run.airShareBefore600 = run.airTimeBefore600 / Mathf.Max(Dt, run.timeBefore600);
            return run;
        }

        static void FinishFlight(Run run, Flight flight, SledPhysics sled, float clearance, bool landed)
        {
            flight.endZ = sled.position.z;
            flight.distance = Mathf.Max(0f, flight.endZ - flight.startZ);
            flight.endSpeed = sled.Speed;
            flight.endClearance = clearance;
            flight.landed = landed;
            flight.landingImpact = landed ? sled.lastImpact : 0f;
            flight.endCause = sled.crashCause.ToString();
            flight.ravine |= sled.crashCause == CrashCause.Ravine || sled.crashCause == CrashCause.Valley;
            flight.meaningful = !flight.ravine && flight.seconds > 0.4f && flight.distance > 8f;
            run.flights.Add(flight);
            if (!flight.meaningful) return;
            run.meaningfulFlights++;
            if (run.firstMeaningfulZ < 0f) run.firstMeaningfulZ = flight.startZ;
            run.longestAirSeconds = Mathf.Max(run.longestAirSeconds, flight.seconds);
            run.longestAirDistance = Mathf.Max(run.longestAirDistance, flight.distance);
        }

        static float FractionBelow(float start, float end, float cutoff)
        {
            if (start >= cutoff) return 0f;
            if (end <= cutoff || end <= start) return 1f;
            return Mathf.Clamp01((cutoff - start) / (end - start));
        }

        static string F(float value) => value.ToString("0.####", Inv);

        static string RunsCsv(Report report)
        {
            var csv = new StringBuilder("mode,level,rocket,player,seed,distance_m,seconds,fill,launch_speed_mps,top_speed_mps,first_meaningful_z,meaningful_flights,longest_air_s,longest_air_m,air_time_s,air_distance_m,air_share_300,air_share_600,air_distance_300,air_distance_600,landings,max_impact_mps,max_clearance_m,rockets_fired,end,cause\n");
            foreach (Run r in report.runs)
                csv.AppendLine(string.Join(",", r.mode, r.level, r.rocketLevel, r.player, r.seed, F(r.distance), F(r.seconds), F(r.fill),
                    F(r.launchSpeed), F(r.topSpeed), F(r.firstMeaningfulZ), r.meaningfulFlights, F(r.longestAirSeconds), F(r.longestAirDistance),
                    F(r.airTime), F(r.airDistance), F(r.airShareBefore300), F(r.airShareBefore600), F(r.airDistanceBefore300),
                    F(r.airDistanceBefore600), r.landings, F(r.maxImpact), F(r.maxClearance), r.rocketsFired, r.end, r.cause));
            return csv.ToString();
        }

        static string FlightsCsv(Report report)
        {
            var csv = new StringBuilder("mode,level,rocket,player,seed,flight,start_z,end_z,seconds,distance_m,start_speed_mps,end_speed_mps,start_clearance_m,end_clearance_m,max_clearance_m,landing_impact_mps,landed,ravine,meaningful,end_cause\n");
            foreach (Run r in report.runs)
                for (int i = 0; i < r.flights.Count; i++)
                {
                    Flight f = r.flights[i];
                    csv.AppendLine(string.Join(",", r.mode, r.level, r.rocketLevel, r.player, r.seed, i + 1, F(f.startZ), F(f.endZ),
                        F(f.seconds), F(f.distance), F(f.startSpeed), F(f.endSpeed), F(f.startClearance), F(f.endClearance), F(f.maxClearance),
                        F(f.landingImpact), f.landed, f.ravine, f.meaningful, f.endCause));
                }
            return csv.ToString();
        }

        static string ProfileCsv(TrackProfile track)
        {
            var csv = new StringBuilder("z_m,path_x_m,height_m,slope,curvature,nominal_detach_speed_mps,bridge,ravine,surface\n");
            for (float z = 0f; z <= track.finishZ; z += 0.5f)
            {
                float x = track.PathX(z), curvature = track.CurvatureAt(x, z);
                float normalGravity = SledPhysics.EarthGravity * track.Normal(x, z).y;
                float threshold = curvature < -0.000001f ? Mathf.Sqrt(normalGravity / -curvature) : -1f;
                csv.AppendLine(string.Join(",", F(z), F(x), F(track.Height(x, z)), F(track.SlopeZ(x, z)), F(curvature), F(threshold),
                    track.BridgeAt(z, out _), track.OverRavine(x, z), track.SurfaceAt(z, x)));
            }
            return csv.ToString();
        }
    }
}
