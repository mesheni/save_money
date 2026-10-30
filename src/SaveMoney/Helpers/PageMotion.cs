using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SaveMoney.Helpers;

/// <summary>Entrance motion belongs to a page visit, never to recycled list rows.</summary>
public static class PageMotion
{
    private static readonly ConditionalWeakTable<ContentPage, Visit> Visits = new();

    public static void Attach(ContentPage page)
    {
        var visit = Visits.GetValue(page, p => new Visit(p));
        visit.Start();
    }

    private sealed class Visit
    {
        private readonly ContentPage _page;
        private readonly HashSet<View> _animated = [];
        private readonly HashSet<View> _observed = [];
        private CancellationTokenSource? _lifetime;

        public Visit(ContentPage page)
        {
            _page = page;
            page.Appearing += (_, _) => Start();
            page.Loaded += (_, _) => Start();
            page.Disappearing += (_, _) => Stop();
        }

        public void Start()
        {
            if (_lifetime is not null || !_page.IsLoaded) return;
            _lifetime = new();
            var token = _lifetime.Token;
            var blocks = Blocks(_page.Content).ToArray();
            foreach (var view in Descendants(_page.Content))
                if (_observed.Add(view)) view.PropertyChanged += OnVisibilityChanged;
            var index = 0;
            foreach (var block in blocks.Where(v => v.IsVisible))
                _ = AnimateAsync(block, Math.Min(index++ * 30, 150), token);
        }

        private void OnVisibilityChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(View.IsVisible) && sender is View { IsVisible: true } view
                && _lifetime is not null)
                _ = AnimateAsync(view, 0, _lifetime.Token);
        }

        private async Task AnimateAsync(View view, int delay, CancellationToken token)
        {
            if (!Motion.AnimationsEnabled || !_animated.Add(view)) return;
            view.Opacity = 0;
            view.TranslationY = 14;
            try
            {
                await Task.Delay(delay, token);
                token.ThrowIfCancellationRequested();
                await Task.WhenAll(view.FadeToAsync(1, Motion.Normal, Motion.Vibrant),
                    view.TranslateToAsync(0, 0, Motion.Normal, Motion.Vibrant));
            }
            catch (OperationCanceledException) { }
            finally
            {
                view.Opacity = 1;
                view.TranslationY = 0;
                _animated.Remove(view);
            }
        }

        private void Stop()
        {
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
            foreach (var view in _observed)
            {
                view.PropertyChanged -= OnVisibilityChanged;
                view.CancelAnimations();
                view.AbortAnimation("PressPulse");
                view.AbortAnimation("CountUp");
                view.Opacity = 1;
                view.TranslationY = 0;
                view.Scale = 1;
            }
            _observed.Clear();
        }

        private static IEnumerable<View> Blocks(View? root)
        {
            if (root is ScrollView scroll)
                return Blocks(scroll.Content);
            if (root is Layout layout)
                return layout.Children.OfType<View>().SelectMany(v => v is ScrollView s ? Blocks(s.Content) : new[] { v });
            return root is null ? [] : [root];
        }

        private static IEnumerable<View> Descendants(View? root)
        {
            if (root is null) yield break;
            yield return root;
            // BindableLayout children and virtualized collection rows have their own lifecycle.
            if (root is Layout layout && BindableLayout.GetItemsSource(layout) is null)
                foreach (var child in layout.Children.OfType<View>())
                    foreach (var view in Descendants(child)) yield return view;
            else if (root is ScrollView scroll)
                foreach (var view in Descendants(scroll.Content)) yield return view;
            else if (root is ContentView content)
                foreach (var view in Descendants(content.Content)) yield return view;
            else if (root is Border border)
                foreach (var view in Descendants(border.Content)) yield return view;
        }
    }
}
