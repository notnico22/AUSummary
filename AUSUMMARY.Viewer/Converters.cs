using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AUSUMMARY.Shared.Models;

namespace AUSUMMARY.Viewer;

/// <summary>
/// Converts a PlayerSnapshot to a character avatar image
/// </summary>
public class PlayerAvatarConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PlayerSnapshot player)
        {
            return CharacterRenderer.CreatePlayerAvatar(player, 48);
        }
        return null!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts role and team to a colored background
/// </summary>
public class RoleColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PlayerSnapshot player)
        {
            var color = CharacterRenderer.GetRoleColor(player.Role, player.Team);
            return new SolidColorBrush(color);
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
