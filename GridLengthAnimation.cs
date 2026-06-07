using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace atsukibrowser
{
    public class GridLengthAnimation : AnimationTimeline
    {
        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register("From", typeof(GridLength), typeof(GridLengthAnimation));
        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register("To", typeof(GridLength), typeof(GridLengthAnimation));

        public GridLength From
        {
            get => (GridLength)GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }
        public GridLength To
        {
            get => (GridLength)GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        public IEasingFunction? EasingFunction { get; set; }

        public override Type TargetPropertyType => typeof(GridLength);

        protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

        public override object GetCurrentValue(object defaultOriginValue,
            object defaultDestinationValue, AnimationClock animationClock)
        {
            double progress = animationClock.CurrentProgress ?? 0;
            if (EasingFunction != null)
                progress = EasingFunction.Ease(progress);

            double from = From.Value;
            double to   = To.Value;
            return new GridLength(from + (to - from) * progress, To.IsStar ? GridUnitType.Star : GridUnitType.Pixel);
        }
    }
}