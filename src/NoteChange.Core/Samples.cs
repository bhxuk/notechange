namespace NoteChange.Core;

/// <summary>Example notes for first-time use, the self-test and screenshots.</summary>
public static class Samples
{
    public static NoteSource Recipe() => NoteSource.FromHtml(
        "<div><b>Lemon drizzle cake</b></div>" +
        "<div>Serves 8 · about an hour</div>" +
        "<div><br></div>" +
        "<div>Ingredients</div>" +
        "<ul><li>225 g soft butter</li><li>225 g caster sugar</li><li>4 eggs</li><li>275 g self-raising flour</li><li>Zest and juice of 2 lemons</li><li>85 g granulated sugar, for the drizzle</li></ul>" +
        "<div>Method</div>" +
        "<ol><li>Heat the oven to 180°C (160°C fan) and line a loaf tin.</li><li>Beat the butter and caster sugar until pale, then add the eggs one at a time.</li><li>Fold in the flour and lemon zest. Spoon into the tin and level the top.</li><li>Bake for 45–50 minutes, until a skewer comes out clean.</li><li>Mix the lemon juice with the granulated sugar and spoon it over the warm cake.</li></ol>" +
        "<div>Notes</div>" +
        "<div><i>Leave it in the tin until completely cool so the drizzle sets with a crunch.</i></div>",
        "Lemon drizzle cake", "example");

    public static NoteSource Chapter() => NoteSource.FromText(
        "Chapter One\n" +
        "The keeper of Gull Point\n" +
        "At dusk the light came on above the harbour, the way it had every night for ninety years. Mara watched it from the kitchen window with a mug going cold in her hands.\n" +
        "Her grandfather had kept the lamp until the day it was automated. After that he kept the logbook anyway, writing down the weather and the boats that passed, as if someone might one day ask.\n" +
        "Nobody ever did. But when the box of logbooks arrived at her door with a note in his careful handwriting, she sat down on the floor of the hallway and began to read.",
        "example");

    public static NoteSource Checklist() => NoteSource.FromHtml(
        "<div>Weekend in the Lakes</div>" +
        "<div>Packing</div>" +
        "<ul class=\"checklist\"><li>☑ Walking boots</li><li>☑ Waterproofs</li><li>☐ Flask and snacks</li><li>☐ OS map for Helvellyn</li></ul>" +
        "<div>Plan</div>" +
        "<table><tr><th>Day</th><th>Walk</th><th>Distance</th></tr><tr><td>Saturday</td><td>Catbells</td><td>3.5 miles</td></tr><tr><td>Sunday</td><td>Tarn Hows</td><td>2 miles</td></tr></table>" +
        "<div>Book the pub for Saturday night: <a href=\"https://example.com\">table for four</a>.</div>",
        "Weekend in the Lakes", "example");
}
