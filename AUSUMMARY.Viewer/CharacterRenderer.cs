using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AUSUMMARY.Shared.Models;

namespace AUSUMMARY.Viewer;

/// <summary>
/// Renders Among Us character avatars based on player cosmetics data
/// </summary>
public static class CharacterRenderer
{
    // Color palette for crewmate colors
    private static readonly Color[] CrewmateColors = new[]
    {
        Color.FromRgb(198, 17, 17),    // Red
        Color.FromRgb(19, 46, 210),    // Blue  
        Color.FromRgb(17, 128, 45),    // Green
        Color.FromRgb(238, 84, 187),   // Pink
        Color.FromRgb(240, 125, 13),   // Orange
        Color.FromRgb(246, 246, 87),   // Yellow
        Color.FromRgb(62, 71, 78),     // Black
        Color.FromRgb(215, 225, 241),  // White
        Color.FromRgb(107, 47, 188),   // Purple
        Color.FromRgb(113, 73, 30),    // Brown
        Color.FromRgb(56, 255, 221),   // Cyan
        Color.FromRgb(80, 240, 57),    // Lime
        Color.FromRgb(108, 47, 188),   // Maroon
        Color.FromRgb(237, 231, 246),  // Rose
        Color.FromRgb(253, 253, 163),  // Banana
        Color.FromRgb(122, 136, 142),  // Gray
        Color.FromRgb(161, 161, 97),   // Tan
        Color.FromRgb(237, 185, 145)   // Coral
    };

    /// <summary>
    /// Creates a simple colored circle representing the player character
    /// </summary>
    public static ImageSource CreatePlayerAvatar(PlayerSnapshot player, int size = 64)
    {
        try
        {
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                // Get player color
                var colorId = Math.Max(0, Math.Min(player.ColorId, CrewmateColors.Length - 1));
                var playerColor = CrewmateColors[colorId];
                
                // Create base crewmate body (circle)
                var bodyBrush = new SolidColorBrush(playerColor);
                var outlineBrush = new SolidColorBrush(Colors.Black);
                
                // Draw body with outline
                context.DrawEllipse(bodyBrush, new Pen(outlineBrush, 2), 
                    new Point(size / 2.0, size / 2.0), size / 2.5, size / 2.5);
                
                // Draw visor (if alive)
                if (player.IsAlive)
                {
                    var visorBrush = new SolidColorBrush(Color.FromArgb(200, 51, 212, 239));
                    var visorRect = new Rect(size * 0.3, size * 0.35, size * 0.4, size * 0.15);
                    context.DrawRoundedRectangle(visorBrush, null, visorRect, 3, 3);
                }
                else
                {
                    // Draw X for dead players
                    var deadPen = new Pen(new SolidColorBrush(Colors.Red), 3);
                    context.DrawLine(deadPen, 
                        new Point(size * 0.3, size * 0.3), 
                        new Point(size * 0.7, size * 0.7));
                    context.DrawLine(deadPen,
                        new Point(size * 0.7, size * 0.3),
                        new Point(size * 0.3, size * 0.7));
                }
                
                // Add backpack indicator
                var backpackBrush = new SolidColorBrush(Darken(playerColor, 0.3));
                context.DrawRectangle(backpackBrush, null, 
                    new Rect(size * 0.65, size * 0.4, size * 0.15, size * 0.25));
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception)
        {
            // Return a default gray circle on error
            return CreateDefaultAvatar(size);
        }
    }

    /// <summary>
    /// Creates a default avatar when rendering fails
    /// </summary>
    private static ImageSource CreateDefaultAvatar(int size)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var grayBrush = new SolidColorBrush(Colors.Gray);
            context.DrawEllipse(grayBrush, new Pen(Brushes.Black, 2),
                new Point(size / 2.0, size / 2.0), size / 2.5, size / 2.5);
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Darkens a color by a specified factor
    /// </summary>
    private static Color Darken(Color color, double factor)
    {
        return Color.FromArgb(
            color.A,
            (byte)(color.R * (1 - factor)),
            (byte)(color.G * (1 - factor)),
            (byte)(color.B * (1 - factor))
        );
    }
    
    /// <summary>
    /// Gets the role color for UI display
    /// </summary>
    public static Color GetRoleColor(string role, string team)
    {
        if (team == "Impostor")
            return Color.FromRgb(255, 25, 25);  // Red for impostors
        
        return role.ToLower() switch
        {
            "engineer" => Color.FromRgb(255, 165, 0),   // Orange
            "scientist" => Color.FromRgb(0, 191, 255),  // Blue
            "guardian angel" => Color.FromRgb(200, 200, 255),  // Light blue
            "shapeshifter" => Color.FromRgb(255, 100, 100),  // Light red
            _ => Color.FromRgb(100, 200, 100)  // Green for standard crewmate
        };
    }
}
