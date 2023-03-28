using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MathNet.Numerics.Statistics;
using Accord.Statistics.Kernels;
using Accord.Statistics.Distributions.Univariate;
using Accord.Math;
using Accord.Statistics;
using System.Globalization;

namespace CoulterCounter
{
    public class CoulterCounterRun
    {
        public string Filename { get; set; }
        public string Sample { get; set; }
        public DateTime Timepoint { get; set; }
        public TimeSpan? RelTime { get; set; }
        public double[] BinLims { get; set; }
        public double[] BinVols { get; set; }
        public double[] BinHeights { get; set; }
        public string YVariable { get; set; }
        public double[] Data { get; set; }
        public double? LivePeak { get; set; }
        public List<double> AllPeaks { get; set; }
        public Dictionary<string, double> Params { get; set; }

        public CoulterCounterRun()
        {
            AllPeaks = new List<double>();
            Params = new Dictionary<string, double>();
        }
    }

    public static class Coulter
    {
        public static double[] RepVec(double[] orig, int[] reps)
        {
            if (orig.Length != reps.Length)
                throw new ArgumentException("Provided arrays have to be of same length");

            var data = new List<double>();
            for (int i = 0; i < orig.Length; i++)
                data.AddRange(Enumerable.Repeat(orig[i], reps[i]));

            return data.ToArray();
        }

        public static double Volume(double diameter)
        {
            return 4.0 / 3.0 * Math.PI * Math.Pow(diameter / 2, 3);
        }

        public static double Diameter(double volume)
        {
            return 2 * Math.Pow(3.0 / 4.0 * volume / Math.PI, 1.0 / 3.0);
        }

        public static Dictionary<string, List<CoulterCounterRun>> LoadFolder(string folderPath)
        {
            var runs = new Dictionary<string, List<CoulterCounterRun>>();
            var files = Directory.GetFiles(folderPath).Where(f => Path.GetExtension(f) == ".Z2").ToArray();

            foreach (var file in files)
            {
                var catName = Path.GetFileName(file).Split('_')[0];
                if (!runs.ContainsKey(catName))
                    runs.Add(catName, new List<CoulterCounterRun>());

                runs[catName].Add(LoadZ2(file, catName));
            }

            foreach (var pair in runs)
                pair.Value.Sort((x, y) => DateTime.Compare(x.Timepoint, y.Timepoint));

            return runs;
        }

        public static CoulterCounterRun LoadZ2(string filepath, string sample, string yvariable = "count")
        {
            // Read and split lines
            var lines = File.ReadAllLines(filepath).Select(line => line.Trim()).ToArray();

            // Extract date and time
            var datetimeLine = lines.FirstOrDefault(line => line.StartsWith("StartTime="));
            var datetimeParts = datetimeLine?.Substring("StartTime=".Length).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var timepoint = DateTime.ParseExact($"{datetimeParts[1]} {datetimeParts[0]}", "HH:mm:ss dd/MMM/yyyy", CultureInfo.InvariantCulture);

            // Extract parameters
            var currentLine = lines.FirstOrDefault(line => line.StartsWith("Cur="));
            var current = double.Parse(currentLine.Substring("Cur=".Length), CultureInfo.InvariantCulture);
            var paramsSection = lines.FirstOrDefault(line => line.StartsWith("Params="));
            var paramParts = paramsSection?.Substring("Params=".Length).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var parameters = paramParts.Select(param => double.Parse(param, CultureInfo.InvariantCulture)).ToArray();

            // Extract bin limits
            var binLimitsSection = lines.FirstOrDefault(line => line.StartsWith("BinLims="));
            var binLimitsParts = binLimitsSection?.Substring("BinLims=".Length).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var binLimits = binLimitsParts.Select(binLimit => double.Parse(binLimit, CultureInfo.InvariantCulture)).ToArray();

            // Extract bin volumes
            var binVolumesSection = lines.FirstOrDefault(line => line.StartsWith("BinVols="));
            var binVolumesParts = binVolumesSection?.Substring("BinVols=".Length).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var binVolumes = binVolumesParts.Select(binVolume => double.Parse(binVolume, CultureInfo.InvariantCulture)).ToArray();

            // Extract bin heights
            var binHeightsSection = lines.FirstOrDefault(line => line.StartsWith("BinHeights="));
            var binHeightsParts = binHeightsSection?.Substring("BinHeights=".Length).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var binHeights = binHeightsParts.Select(binHeight => double.Parse(binHeight, CultureInfo.InvariantCulture)).ToArray();

            // Calculate data
            var data = RepVec(binLimits.Skip(1).ToArray(), binHeights.Select(Convert.ToInt32).ToArray());

            return new CoulterCounterRun
            {
                Filename = Path.GetFileName(filepath),
                Sample = sample,
                Timepoint = timepoint,
                BinLims = binLimits,
                BinVols = binVolumes,
                BinHeights = binHeights,
                YVariable = yvariable,
                Data = data,
                Params = new Dictionary<string, double>
                {
                    { "Current", current },
                    { "Threshold", parameters[0] },
                    { "Diameter", parameters[1] },
                    { "K", parameters[2] },
                    { "Chi2", parameters[3] },
                }
            };
        }
    }
}