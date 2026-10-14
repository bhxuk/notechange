using System;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace NoteChange
{
    /// <summary>The NoteChange look: the same dark, warm palette as the other bhxuk apps.</summary>
    public static class Theme
    {
        public static readonly Color BgColor = Color.FromRgb(0x0F, 0x11, 0x15);
        public static readonly Color PanelColor = Color.FromRgb(0x18, 0x1B, 0x22);
        public static readonly Color SideColor = Color.FromRgb(0x14, 0x17, 0x1D);
        public static readonly Color StageColor = Color.FromRgb(0x0B, 0x0D, 0x11);
        public static readonly Color TextColor = Color.FromRgb(0xF3, 0xF1, 0xEC);
        public static readonly Color MutedColor = Color.FromRgb(0xA7, 0xAB, 0xB5);
        public static readonly Color AmberColor = Color.FromRgb(0xFF, 0xB5, 0x47);
        public static readonly Color TealColor = Color.FromRgb(0x5E, 0xD3, 0xC3);
        public static readonly Color RedColor = Color.FromRgb(0xE2, 0x58, 0x5F);

        public static readonly SolidColorBrush Bg = Freeze(BgColor);
        public static readonly SolidColorBrush Panel = Freeze(PanelColor);
        public static readonly SolidColorBrush Side = Freeze(SideColor);
        public static readonly SolidColorBrush Stage = Freeze(StageColor);
        public static readonly SolidColorBrush Text = Freeze(TextColor);
        public static readonly SolidColorBrush Muted = Freeze(MutedColor);
        public static readonly SolidColorBrush Faint = Freeze(Color.FromRgb(0x6E, 0x73, 0x7E));
        public static readonly SolidColorBrush Amber = Freeze(AmberColor);
        public static readonly SolidColorBrush Teal = Freeze(TealColor);
        public static readonly SolidColorBrush Red = Freeze(RedColor);
        public static readonly SolidColorBrush Line = Freeze(Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF));
        public static readonly SolidColorBrush Subtle = Freeze(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF));
        public static readonly SolidColorBrush AmberTint = Freeze(Color.FromArgb(0x26, 0xFF, 0xB5, 0x47));
        public static readonly SolidColorBrush Dark = Freeze(Color.FromRgb(0x1A, 0x12, 0x06));

        public static readonly FontFamily Font = new FontFamily("Segoe UI Variable Text, Segoe UI");
        public static readonly FontFamily Display = new FontFamily("Segoe UI Variable Display, Segoe UI");

        private static SolidColorBrush Freeze(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        /// <summary>Loads the control styles into the application's resources.</summary>
        public static void Load(Application app)
        {
            var dict = (ResourceDictionary)XamlReader.Parse(Styles);
            app.Resources.MergedDictionaries.Add(dict);
        }

        private const string Styles = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <SolidColorBrush x:Key='Text' Color='#F3F1EC'/>
  <SolidColorBrush x:Key='Muted' Color='#A7ABB5'/>
  <SolidColorBrush x:Key='Amber' Color='#FFB547'/>
  <SolidColorBrush x:Key='Line' Color='#16FFFFFF'/>
  <SolidColorBrush x:Key='Subtle' Color='#10FFFFFF'/>

  <Style x:Key='Base' TargetType='Button'>
    <Setter Property='Foreground' Value='#F3F1EC'/>
    <Setter Property='Background' Value='#10FFFFFF'/>
    <Setter Property='BorderBrush' Value='#16FFFFFF'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='14,9'/>
    <Setter Property='FontSize' Value='13.5'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='HorizontalContentAlignment' Value='Center'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Grid>
            <Border x:Name='bg' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                    BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='9'/>
            <Border x:Name='hover' Background='#14FFFFFF' CornerRadius='9' Opacity='0'/>
            <Border x:Name='focus' BorderBrush='#FFB547' BorderThickness='2' CornerRadius='10' Margin='-3' Opacity='0'/>
            <ContentPresenter Margin='{TemplateBinding Padding}' HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'
                              VerticalAlignment='Center' RecognizesAccessKey='True'/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='hover' Property='Opacity' Value='1'/></Trigger>
            <Trigger Property='IsPressed' Value='True'><Setter TargetName='hover' Property='Opacity' Value='1'/><Setter TargetName='bg' Property='Opacity' Value='0.85'/></Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='focus' Property='Opacity' Value='1'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='Button' BasedOn='{StaticResource Base}'/>
  <Style x:Key='Secondary' TargetType='Button' BasedOn='{StaticResource Base}'/>
  <Style x:Key='Primary' TargetType='Button' BasedOn='{StaticResource Base}'>
    <Setter Property='Background' Value='#FFB547'/>
    <Setter Property='BorderBrush' Value='#FFB547'/>
    <Setter Property='Foreground' Value='#1A1206'/>
    <Setter Property='FontWeight' Value='Bold'/>
  </Style>
  <Style x:Key='Quiet' TargetType='Button' BasedOn='{StaticResource Base}'>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='BorderBrush' Value='Transparent'/>
    <Setter Property='Foreground' Value='#A7ABB5'/>
    <Setter Property='FontWeight' Value='Normal'/>
    <Setter Property='Padding' Value='10,6'/>
    <Setter Property='FontSize' Value='12.5'/>
  </Style>

  <Style TargetType='TextBox'>
    <Setter Property='Foreground' Value='#F3F1EC'/>
    <Setter Property='Background' Value='#0F1115'/>
    <Setter Property='BorderBrush' Value='#22FFFFFF'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='10,0'/>
    <Setter Property='Height' Value='38'/>
    <Setter Property='VerticalContentAlignment' Value='Center'/>
    <Setter Property='FontSize' Value='13.5'/>
    <Setter Property='CaretBrush' Value='#FFB547'/>
    <Setter Property='SelectionBrush' Value='#FFB547'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='TextBox'>
          <Border x:Name='b' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                  BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='9'>
            <ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='#FFB547'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='CheckBox'>
    <Setter Property='Foreground' Value='#F3F1EC'/>
    <Setter Property='FontSize' Value='13.5'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='CheckBox'>
          <Grid Background='Transparent'>
            <Grid.ColumnDefinitions><ColumnDefinition Width='Auto'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
            <Border x:Name='track' Width='36' Height='20' CornerRadius='10' Background='#3A3F4B' VerticalAlignment='Top' Margin='0,1,10,0'>
              <Ellipse x:Name='knob' Width='14' Height='14' Fill='#F3F1EC' HorizontalAlignment='Left' Margin='3,0,0,0'/>
            </Border>
            <Border x:Name='focus' Grid.Column='0' Width='42' Height='26' CornerRadius='13' BorderBrush='#FFB547' BorderThickness='2'
                    VerticalAlignment='Top' HorizontalAlignment='Left' Margin='-3,-2,0,0' Opacity='0'/>
            <ContentPresenter Grid.Column='1' VerticalAlignment='Center' RecognizesAccessKey='True'/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='track' Property='Background' Value='#FFB547'/>
              <Setter TargetName='knob' Property='HorizontalAlignment' Value='Right'/>
              <Setter TargetName='knob' Property='Margin' Value='0,0,3,0'/>
              <Setter TargetName='knob' Property='Fill' Value='#1A1206'/>
            </Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='focus' Property='Opacity' Value='1'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='Segment' TargetType='RadioButton'>
    <Setter Property='Foreground' Value='#A7ABB5'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Margin' Value='0,0,6,6'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='RadioButton'>
          <Grid>
            <Border x:Name='b' Background='#10FFFFFF' BorderBrush='#16FFFFFF' BorderThickness='1' CornerRadius='9' Padding='10,8'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' RecognizesAccessKey='True'/>
            </Border>
            <Border x:Name='focus' BorderBrush='#FFB547' BorderThickness='2' CornerRadius='10' Margin='-3' Opacity='0'/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#1CFFFFFF'/></Trigger>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='b' Property='Background' Value='#26FFB547'/>
              <Setter TargetName='b' Property='BorderBrush' Value='#FFB547'/>
              <Setter Property='Foreground' Value='#F3F1EC'/>
            </Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='focus' Property='Opacity' Value='1'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='ListBox'>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='Foreground' Value='#F3F1EC'/>
    <Setter Property='ScrollViewer.HorizontalScrollBarVisibility' Value='Disabled'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
  </Style>

  <Style TargetType='ListBoxItem'>
    <Setter Property='Foreground' Value='#F3F1EC'/>
    <Setter Property='Margin' Value='0,0,0,4'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ListBoxItem'>
          <Border x:Name='b' Background='Transparent' BorderBrush='Transparent' BorderThickness='1' CornerRadius='9' Padding='12,9'>
            <ContentPresenter/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#10FFFFFF'/></Trigger>
            <Trigger Property='IsSelected' Value='True'>
              <Setter TargetName='b' Property='Background' Value='#26FFB547'/>
              <Setter TargetName='b' Property='BorderBrush' Value='#66FFB547'/>
            </Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='#FFB547'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='ScrollBar'>
    <Setter Property='Width' Value='8'/>
    <Setter Property='MinWidth' Value='8'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ScrollBar'>
          <Track x:Name='PART_Track' IsDirectionReversed='True'>
            <Track.Thumb>
              <Thumb>
                <Thumb.Template>
                  <ControlTemplate TargetType='Thumb'><Border Background='#33FFFFFF' CornerRadius='4' Margin='1,2'/></ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
          </Track>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='ToolTip'>
    <Setter Property='Background' Value='#181B22'/>
    <Setter Property='Foreground' Value='#F3F1EC'/>
    <Setter Property='BorderBrush' Value='#22FFFFFF'/>
    <Setter Property='Padding' Value='10,6'/>
  </Style>
</ResourceDictionary>";
    }
}
