using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeamNGTerrainGenerator;

internal static class ExceptionBox
{
    public static void Show(Exception e)
    {
        MessageBox.Show(e.Message, e.GetType().Name, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public static void Try(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Show(e);
        }
    }
}
