# Single declaration of the plugin suite, in dependency order.
@{
    Plugins = @(
        @{ Name = 'InnoPlugin.VisualNovel.Narrative'; Id = 'innoplugin.visualnovel.narrative'; Dependencies = @() }
        @{ Name = 'InnoPlugin.VisualNovel.Stage'; Id = 'innoplugin.visualnovel.stage'; Dependencies = @() }
        @{ Name = 'InnoPlugin.VisualNovel.UI'; Id = 'innoplugin.visualnovel.ui'; Dependencies = @() }
        @{
            Name = 'InnoPlugin.VisualNovel'
            Id = 'innoplugin.visualnovel'
            Dependencies = @(
                'innoplugin.visualnovel.narrative'
                'innoplugin.visualnovel.stage'
                'innoplugin.visualnovel.ui'
            )
        }
    )
}
