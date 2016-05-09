using System;
using System.Threading.Tasks;
using UIKit;
using CoreGraphics;

namespace ZXing.Mobile
{
    public sealed class ZXingScannerViewController : UIViewController, IScannerViewController
    {
        private ZXingScannerView _scannerView;

        public event Action<Result> OnScannedResult;

        public MobileBarcodeScanningOptions ScanningOptions { get; set; }
        public MobileBarcodeScanner Scanner { get; set; }
        public bool ContinuousScanning { get; set; }

        private UIActivityIndicatorView _loadingView;
        private UIView _loadingBg;

        public UIView CustomLoadingView { get; set; }

        public ZXingScannerViewController(MobileBarcodeScanningOptions options, MobileBarcodeScanner scanner)
        {
            ScanningOptions = options;
            Scanner = scanner;

            var appFrame = UIScreen.MainScreen.ApplicationFrame;

            View.Frame = new CGRect(0, 0, appFrame.Width, appFrame.Height);
            View.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
        }

        public UIViewController AsViewController()
        {
            return this;
        }

        public void Cancel()
        {
            InvokeOnMainThread(_scannerView.StopScanning);
        }

        private UIStatusBarStyle _originalStatusBarStyle = UIStatusBarStyle.Default;

        public override void ViewDidLoad()
        {
            _loadingBg = new UIView(View.Frame) { BackgroundColor = UIColor.Black, AutoresizingMask = UIViewAutoresizing.FlexibleDimensions };
            _loadingView = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.WhiteLarge) { AutoresizingMask = UIViewAutoresizing.FlexibleMargins };
            _loadingView.Frame = new CGRect((View.Frame.Width - _loadingView.Frame.Width) / 2,
                                            (View.Frame.Height - _loadingView.Frame.Height) / 2,
                                            _loadingView.Frame.Width,
                                            _loadingView.Frame.Height);

            _loadingBg.AddSubview(_loadingView);
            View.AddSubview(_loadingBg);
            _loadingView.StartAnimating();

            _scannerView = new ZXingScannerView(new CGRect(0, 0, View.Frame.Width, View.Frame.Height))
            {
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
                UseCustomOverlayView = Scanner.UseCustomOverlay,
                CustomOverlayView = Scanner.CustomOverlay,
                TopText = Scanner.TopText,
                BottomText = Scanner.BottomText,
                CancelButtonText = Scanner.CancelButtonText,
                FlashButtonText = Scanner.FlashButtonText
            };
            _scannerView.OnCancelButtonPressed += () => Scanner.Cancel();

            View.InsertSubviewBelow(_scannerView, _loadingView);
            View.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
        }

        public void Torch(bool on)
        {
            _scannerView?.Torch(on);
        }

        public void ToggleTorch()
        {
            _scannerView?.ToggleTorch();
        }

        public void PauseAnalysis()
        {
            _scannerView.PauseAnalysis();
        }

        public void ResumeAnalysis()
        {
            _scannerView.ResumeAnalysis();
        }

        public bool IsTorchOn => _scannerView.IsTorchOn;

        public override void ViewDidAppear(bool animated)
        {
            _scannerView.OnScannerSetupComplete += HandleOnScannerSetupComplete;

            _originalStatusBarStyle = UIApplication.SharedApplication.StatusBarStyle;

            if (UIDevice.CurrentDevice.CheckSystemVersion(7, 0))
            {
                UIApplication.SharedApplication.StatusBarStyle = UIStatusBarStyle.Default;
                SetNeedsStatusBarAppearanceUpdate();
            }
            else
                UIApplication.SharedApplication.SetStatusBarStyle(UIStatusBarStyle.BlackTranslucent, false);

            Console.WriteLine("Starting to scan...");

            Task.Factory.StartNew(() =>
            {
                BeginInvokeOnMainThread(() => _scannerView.StartScanning(result =>
                {
                    if (!ContinuousScanning)
                    {
                        Console.WriteLine("Stopping scan...");
                        _scannerView.StopScanning();
                    }

                    var evt = OnScannedResult;
                    evt?.Invoke(result);
                }, ScanningOptions));
            });
        }

        public override void ViewDidDisappear(bool animated)
        {
            _scannerView?.StopScanning();
            _scannerView.OnScannerSetupComplete -= HandleOnScannerSetupComplete;
        }

        public override void ViewWillDisappear(bool animated)
        {
            UIApplication.SharedApplication.SetStatusBarStyle(_originalStatusBarStyle, false);
        }

        public override void DidRotate(UIInterfaceOrientation fromInterfaceOrientation)
        {
            _scannerView?.DidRotate(InterfaceOrientation);
        }

        public override bool ShouldAutorotate() => true;

        public override UIInterfaceOrientationMask GetSupportedInterfaceOrientations() => Scanner.Orientation;

        private void HandleOnScannerSetupComplete()
        {
            BeginInvokeOnMainThread(() =>
            {
                if (_loadingView != null && _loadingBg != null && _loadingView.IsAnimating)
                {
                    _loadingView.StopAnimating();

                    UIView.BeginAnimations("zoomout");

                    UIView.SetAnimationDuration(2.0f);
                    UIView.SetAnimationCurve(UIViewAnimationCurve.EaseOut);

                    _loadingBg.Transform = CGAffineTransform.MakeScale(2.0f, 2.0f);
                    _loadingBg.Alpha = 0.0f;

                    UIView.CommitAnimations();

                    _loadingBg.RemoveFromSuperview();
                }
            });
        }
    }
}

