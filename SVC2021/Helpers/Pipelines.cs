using SigStat.Common;
using SigStat.Common.Pipeline;
using SigStat.Common.PipelineItems.Transforms.Preprocessing;
using System;
using System.Collections.Generic;
using System.Text;

namespace SVC2021.Helpers
{
    public static class Pipelines
    {
        public static readonly ITransformation Filter = new FilterPoints
        {
            InputFeatures = new List<FeatureDescriptor<List<double>>> { Features.X, Features.Y, Features.T },
            OutputFeatures = new List<FeatureDescriptor<List<double>>> { Features.X, Features.Y, Features.T },
            KeyFeatureInput = Features.Pressure,
            KeyFeatureOutput = Features.Pressure
        };

        public static readonly ITransformation Scale1X = new Scale() { InputFeature = Features.X, OutputFeature = Features.X, Mode = ScalingMode.Scaling1 };
        public static readonly ITransformation Scale1Y = new Scale() { InputFeature = Features.Y, OutputFeature = Features.Y, Mode = ScalingMode.Scaling1 };
        public static readonly ITransformation Scale1Pressure = new Scale() { InputFeature = Features.Pressure, OutputFeature = Features.Pressure, Mode = ScalingMode.Scaling1 };

        public static readonly ITransformation SvcScale1X = new SvcScale() { InputFeature = Features.X, OutputFeature = Features.X, Mode = ScalingMode.Scaling1 };
        public static readonly ITransformation SvcScale1Y = new SvcScale() { InputFeature = Features.Y, OutputFeature = Features.Y, Mode = ScalingMode.Scaling1 };
        public static readonly ITransformation SvcScale1Pressure = new SvcScale() { InputFeature = Features.Pressure, OutputFeature = Features.Pressure, Mode = ScalingMode.Scaling1 };

        public static readonly ITransformation ScaleSX = new Scale() { InputFeature = Features.X, OutputFeature = Features.X, Mode = ScalingMode.ScalingS };
        public static readonly ITransformation ScaleSY = new Scale() { InputFeature = Features.Y, OutputFeature = Features.Y, Mode = ScalingMode.ScalingS };
        public static readonly ITransformation ScaleSPressure = new Scale() { InputFeature = Features.Pressure, OutputFeature = Features.Pressure, Mode = ScalingMode.ScalingS };

        public static readonly ITransformation TranslateCogX = new TranslatePreproc(OriginType.CenterOfGravity) { InputFeature = Features.X, OutputFeature = Features.X };
        public static readonly ITransformation TranslateCogY = new TranslatePreproc(OriginType.CenterOfGravity) { InputFeature = Features.Y, OutputFeature = Features.Y };
        public static readonly ITransformation TranslateCogPressure = new TranslatePreproc(OriginType.CenterOfGravity) { InputFeature = Features.Pressure, OutputFeature = Features.Pressure };

        public static readonly ITransformation Rotation1 = new NormalizeRotation() { InputX = Features.X, InputY = Features.Y, OutputX = Features.X, OutputY = Features.Y, InputT = Features.T };
        public static readonly ITransformation Rotation2 = new NormalizeRotation2() { InputX = Features.X, InputY = Features.Y, OutputX = Features.X, OutputY = Features.Y };

        public static readonly ITransformation FilterScale1TranslateCogXYP = new SequentialTransformPipeline()
            {
                Filter,
                Scale1X,
                Scale1Y,
                Scale1Pressure,
                TranslateCogX,
                TranslateCogY,
                TranslateCogPressure
            };

        public static readonly ITransformation FilterScale1TranslateCogXY = new SequentialTransformPipeline()
            {
                Filter,
                Scale1X,
                Scale1Y,
                TranslateCogX,
                TranslateCogY,
            };

        public static readonly ITransformation Rotation2FilterScale1TranslateCogXYP = new SequentialTransformPipeline()
            {
                Rotation2,
                Filter,
                Scale1X,
                Scale1Y,
                Scale1Pressure,
                TranslateCogX,
                TranslateCogY,
                TranslateCogPressure
            };

        public static readonly ITransformation FilterScale1TranslateCogRotation2XYP = new SequentialTransformPipeline()
            {
                Filter,
                Scale1X,
                Scale1Y,
                Scale1Pressure,
                TranslateCogX,
                TranslateCogY,
                TranslateCogPressure,
                Rotation2
            };

        public static readonly ITransformation FilterScale1TranslateCogRotation1XYP = new SequentialTransformPipeline()
            {
                Filter,
                Scale1X,
                Scale1Y,
                Scale1Pressure,
                TranslateCogX,
                TranslateCogY,
                TranslateCogPressure,
                Rotation1
            };
    }
}