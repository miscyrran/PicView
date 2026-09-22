using PicView.Core.Config;
using R3;

namespace PicView.Core.ViewModels;

public class ImageInfoWindowViewModel : IDisposable
{
    public ImageInfoWindowConfig? ImageInfoWindowConfig { get; set; }
    
    public int TextWidth => 100;
    public int CopyButtonWidth => 37;
    
    private const int TextMaxWidth = 135;
    
    public BindableReactiveProperty<double> TextBoxMaxWidth { get; } = new(TextMaxWidth);
    public BindableReactiveProperty<double> ExtraTextBoxMaxWidth { get; } = new(TextMaxWidth);

    public BindableReactiveProperty<double> TextBoxWidth { get; } = new(180);
    public BindableReactiveProperty<double> TextBoxXlWidth { get; } = new(620);
    public BindableReactiveProperty<double> TextBoxXxlWidth { get; } = new(630);
    public BindableReactiveProperty<double> HalfLineWidth { get; } = new(395);

    /// <summary>
    /// Full width for the Stable Diffusion rows, whose label and buttons sit above the text box so
    /// the buttons stay visible however narrow the window is.
    /// </summary>
    public BindableReactiveProperty<double> SdRowWidth { get; } = new(760);

    public BindableReactiveProperty<bool> IsCopyButtonEnabled { get; } = new();
    public BindableReactiveProperty<bool> IsExtraButtonsEnabled { get; } = new();
    
    public BindableReactiveProperty<bool> IsLoading { get; } = new(true);

    public void ResponsiveResizeUpdate(double width, double scrollBarThickness)
    {
        const int firstBreakPoint = 600;
        const int secondBreakPoint = 915;
        const int thirdBreakPoint = 1150;
        const int fourthBreakPoint = 1450;

        var textWidth = TextWidth;
        var copyBtnWidth = CopyButtonWidth;

        IsCopyButtonEnabled.Value = width >= firstBreakPoint;
        IsExtraButtonsEnabled.Value = width >= secondBreakPoint;

        const int padding = 10;

        TextBoxMaxWidth.Value = width < thirdBreakPoint ? 0 : TextMaxWidth;
        ExtraTextBoxMaxWidth.Value = width < fourthBreakPoint ? 0 : TextMaxWidth;

        switch (width)
        {
            case <= firstBreakPoint:
                TextBoxWidth.Value = TextBoxXlWidth.Value = width - (textWidth + scrollBarThickness + padding);
                TextBoxXxlWidth.Value = width - (textWidth + scrollBarThickness);
                break;
            case >= firstBreakPoint and <= secondBreakPoint:
                TextBoxWidth.Value = TextBoxXlWidth.Value = TextBoxXxlWidth.Value =
                    width - (textWidth + scrollBarThickness + padding + copyBtnWidth);
                break;
            case >= secondBreakPoint and <= thirdBreakPoint:
                var thirdBreakWidth = width - width / 2 - (textWidth * 2 + scrollBarThickness + padding) +
                                      copyBtnWidth * 2;
                TextBoxWidth.Value = thirdBreakWidth;
                var newWidthBreakL =  thirdBreakWidth * 2 + textWidth * 2 - padding * 2;
                TextBoxXlWidth.Value = newWidthBreakL;
                TextBoxXxlWidth.Value = newWidthBreakL - padding;
                break;
            case >= thirdBreakPoint:
                var aboveThirdWidth = width / 2 - (textWidth * 2 + scrollBarThickness) +
                                      copyBtnWidth;
                TextBoxWidth.Value = aboveThirdWidth;
                var aboveThirdWidthL = aboveThirdWidth * 2 + textWidth * 2 - padding * 2;
                TextBoxXlWidth.Value = aboveThirdWidthL;
                TextBoxXxlWidth.Value = aboveThirdWidthL - padding;
                break;
        }

        // Row margin (10 each side), wrap panel and parent margins, plus the scroll bar.
        SdRowWidth.Value = Math.Max(150, width - (scrollBarThickness + 40));

        if (width >= thirdBreakPoint)
        {
            HalfLineWidth.Value = width / 2 - (scrollBarThickness + padding + 15);
        }
        else
        {
            HalfLineWidth.Value = width / 2 - (scrollBarThickness + padding);
        }
    }
    
    public void Dispose()
    {
        Disposable.Dispose(
            TextBoxMaxWidth,
            TextBoxMaxWidth,
            TextBoxWidth,
            TextBoxXlWidth,
            TextBoxXxlWidth,
            HalfLineWidth,
            SdRowWidth,
            IsCopyButtonEnabled,
            IsExtraButtonsEnabled,
            IsLoading);
        
        GC.SuppressFinalize(this);
    }
}