namespace SaveMoney.Helpers;

public static class PageNavigation
{
    public static Task GoToAsync(string route) => Shell.Current.GoToAsync(route, false);
}
