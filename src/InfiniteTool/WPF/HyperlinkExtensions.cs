﻿using System.Diagnostics;

namespace InfiniteTool.WPF
{
    //public static class HyperlinkExtensions
    //{
    //    public static bool GetIsExternal(DependencyObject obj)
    //    {
    //        return (bool)obj.GetValue(IsExternalProperty);
    //    }
    //
    //    public static void SetIsExternal(DependencyObject obj, bool value)
    //    {
    //        obj.SetValue(IsExternalProperty, value);
    //    }
    //
    //    public static readonly DependencyProperty IsExternalProperty =
    //        DependencyProperty.RegisterAttached("IsExternal", typeof(bool), typeof(HyperlinkExtensions), new UIPropertyMetadata(false, OnIsExternalChanged));
    //
    //    private static void OnIsExternalChanged(object sender, DependencyPropertyChangedEventArgs args)
    //    {
    //        var hyperlink = sender as Hyperlink;
    //
    //        if ((bool)args.NewValue)
    //            hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
    //        else
    //            hyperlink.RequestNavigate -= Hyperlink_RequestNavigate;
    //    }
    //
    //    private static void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    //    {
    //        var info = new ProcessStartInfo(e.Uri.AbsoluteUri);
    //        info.UseShellExecute = true;
    //        Process.Start(info);
    //        e.Handled = true;
    //    }
    //}
}
