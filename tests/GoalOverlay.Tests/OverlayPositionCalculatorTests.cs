using System.Windows;
using GoalOverlay.Models;
using GoalOverlay.Native;
using Xunit;

namespace GoalOverlay.Tests;

public class OverlayPositionCalculatorTests
{
    [Theory]
    [InlineData(1920, 1080, 300, 150, 1604, 16)]     // 1080p
    [InlineData(2560, 1440, 320, 180, 2224, 16)]     // 1440p 2K
    [InlineData(3840, 2160, 350, 200, 3474, 16)]     // 2160p 4K
    public void CalculatePosition_TopRight_Dpi100Percent_PlacesAtTopRightWith16pxMargin(
        double screenW, double screenH, double winW, double winH, double expectedX, double expectedY)
    {
        var workArea = new Rect(0, 0, screenW, screenH);
        var pos = OverlayPositionCalculator.CalculatePosition(workArea, winW, winH, ScreenCorner.TopRight, margin: 16);

        Assert.Equal(expectedX, pos.X);
        Assert.Equal(expectedY, pos.Y);
    }

    [Fact]
    public void CalculatePosition_AllFourCorners_CalculateCorrectly()
    {
        var workArea = new Rect(0, 0, 1920, 1080);
        double winW = 300;
        double winH = 150;
        double margin = 16;

        var topLeft = OverlayPositionCalculator.CalculatePosition(workArea, winW, winH, ScreenCorner.TopLeft, margin);
        Assert.Equal(16, topLeft.X);
        Assert.Equal(16, topLeft.Y);

        var topRight = OverlayPositionCalculator.CalculatePosition(workArea, winW, winH, ScreenCorner.TopRight, margin);
        Assert.Equal(1920 - winW - 16, topRight.X);
        Assert.Equal(16, topRight.Y);

        var bottomLeft = OverlayPositionCalculator.CalculatePosition(workArea, winW, winH, ScreenCorner.BottomLeft, margin);
        Assert.Equal(16, bottomLeft.X);
        Assert.Equal(1080 - winH - 16, bottomLeft.Y);

        var bottomRight = OverlayPositionCalculator.CalculatePosition(workArea, winW, winH, ScreenCorner.BottomRight, margin);
        Assert.Equal(1920 - winW - 16, bottomRight.X);
        Assert.Equal(1080 - winH - 16, bottomRight.Y);
    }

    [Fact]
    public void ConvertPhysicalWorkAreaToDips_Dpi125Percent_ScalesCorrectly()
    {
        // Độ phân giải 1920x1080 tại DPI 125% (scale = 1.25)
        // 1920 / 1.25 = 1536 DIPs; 1080 / 1.25 = 864 DIPs
        var dipsArea = OverlayPositionCalculator.ConvertPhysicalWorkAreaToDips(0, 0, 1920, 1080, 1.25, 1.25);

        Assert.Equal(0, dipsArea.Left);
        Assert.Equal(0, dipsArea.Top);
        Assert.Equal(1536, dipsArea.Width);
        Assert.Equal(864, dipsArea.Height);

        // Đặt window kích thước 300x150 DIPs
        var pos = OverlayPositionCalculator.CalculatePosition(dipsArea, 300, 150, ScreenCorner.TopRight, margin: 16);
        Assert.Equal(1536 - 300 - 16, pos.X); // 1220
        Assert.Equal(16, pos.Y);
    }

    [Fact]
    public void ConvertPhysicalWorkAreaToDips_Dpi150Percent_ScalesCorrectly()
    {
        // 1920x1080 tại DPI 150% (scale = 1.5)
        // 1920 / 1.5 = 1280 DIPs; 1080 / 1.5 = 720 DIPs
        var dipsArea = OverlayPositionCalculator.ConvertPhysicalWorkAreaToDips(0, 0, 1920, 1080, 1.5, 1.5);

        Assert.Equal(1280, dipsArea.Width);
        Assert.Equal(720, dipsArea.Height);

        var pos = OverlayPositionCalculator.CalculatePosition(dipsArea, 280, 140, ScreenCorner.TopRight, margin: 16);
        Assert.Equal(1280 - 280 - 16, pos.X); // 984
        Assert.Equal(16, pos.Y);
    }

    [Fact]
    public void ConvertPhysicalWorkAreaToDips_Dpi200Percent_ScalesCorrectly()
    {
        // Màn hình 4K 3840x2160 tại DPI 200% (scale = 2.0)
        // 3840 / 2.0 = 1920 DIPs; 2160 / 2.0 = 1080 DIPs
        var dipsArea = OverlayPositionCalculator.ConvertPhysicalWorkAreaToDips(0, 0, 3840, 2160, 2.0, 2.0);

        Assert.Equal(1920, dipsArea.Width);
        Assert.Equal(1080, dipsArea.Height);

        var pos = OverlayPositionCalculator.CalculatePosition(dipsArea, 350, 180, ScreenCorner.TopRight, margin: 16);
        Assert.Equal(1920 - 350 - 16, pos.X); // 1554
        Assert.Equal(16, pos.Y);
    }

    [Fact]
    public void DualMonitor_SecondaryScreen_CalculatesCorrectPositionWithOffset()
    {
        // Màn hình 2 nằm bên phải màn hình 1:
        // Màn hình 1: 0..1920
        // Màn hình 2: Left = 1920, Right = 3840, Top = 0, Bottom = 1080 tại 100% DPI
        var secondaryWorkArea = OverlayPositionCalculator.ConvertPhysicalWorkAreaToDips(1920, 0, 3840, 1080, 1.0, 1.0);

        Assert.Equal(1920, secondaryWorkArea.Left);
        Assert.Equal(1920, secondaryWorkArea.Width);

        double winW = 300;
        double winH = 150;
        var pos = OverlayPositionCalculator.CalculatePosition(secondaryWorkArea, winW, winH, ScreenCorner.TopRight, margin: 16);

        // Vị trí góc phải màn hình 2: 3840 - 300 - 16 = 3524
        Assert.Equal(3524, pos.X);
        Assert.Equal(16, pos.Y);
    }

    [Fact]
    public void WindowLargerThanWorkArea_ClampsToWorkAreaBoundaries()
    {
        // Màn hình siêu nhỏ 200x200, window 400x400
        var workArea = new Rect(0, 0, 200, 200);
        var pos = OverlayPositionCalculator.CalculatePosition(workArea, 400, 400, ScreenCorner.TopRight, margin: 16);

        // Phải được clamp về Left = 0, Top = 16
        Assert.Equal(0, pos.X);
        Assert.Equal(16, pos.Y);
    }

    [Fact]
    public void InvalidDpi_FallbackToScale1()
    {
        // Nếu hệ điều hành trả về DPI <= 0, tự động fallback về scale 1.0 không gây lỗi chia cho 0
        var dipsArea = OverlayPositionCalculator.ConvertPhysicalWorkAreaToDips(0, 0, 1920, 1080, 0, -1);
        Assert.Equal(1920, dipsArea.Width);
        Assert.Equal(1080, dipsArea.Height);
    }
}
