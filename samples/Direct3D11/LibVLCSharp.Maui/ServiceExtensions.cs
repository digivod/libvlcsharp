namespace LibVLCSharp.Maui;

public static class ServiceExtensions
{

    public static MauiAppBuilder AddMauiLibVLCSharp(this MauiAppBuilder builder)
    {
        builder.ConfigureMauiHandlers(collection =>
        {
            collection.AddHandler(typeof(VideoView), typeof(VideoViewHandler));
        });        
        return builder;
    }
}