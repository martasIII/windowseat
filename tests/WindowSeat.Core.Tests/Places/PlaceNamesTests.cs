using WindowSeat.Core.Places;

namespace WindowSeat.Core.Tests.Places;

public class PlaceNamesTests
{
    [Theory]
    [InlineData("katedrála svatého Víta v Praze", "Katedrála sv. Víta")]
    [InlineData("Kostel svatého Mikuláše na Malé Straně", "Kostel sv. Mikuláše na Malé Straně")]
    [InlineData("kostel svatého Mikuláše v Praze-Starém Městě", "Kostel sv. Mikuláše")]
    [InlineData("Bazilika svatých Petra a Pavla na Vyšehradě", "Bazilika sv. Petra a Pavla na Vyšehradě")]
    [InlineData("Bazilika svaté Ludmily", "Bazilika sv. Ludmily")]
    [InlineData("Valdštejnský palác v Praze", "Valdštejnský palác")]
    [InlineData("Kostel svatého Fabiána (Praha)", "Kostel sv. Fabiána")]
    [InlineData("Karlův most", "Karlův most")]
    [InlineData("Svatá Hora", "Svatá Hora")]
    public void Shortens_city_context_and_saints(string name, string expected)
    {
        Assert.Equal(expected, PlaceNames.Shorten(name));
    }
}
