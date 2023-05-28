using Newtonsoft.Json.Linq;
using System.Collections.Generic;

// These numbers represents the Ids of all members under the UN.Orientation Taxonomy
enum Orientation {
    Landscape = 37716,
    Portrait = 37717,
    Square = 37718
}

var width = 0;
var height = 0;
var entity = Context.Asset;
var properties = Context.MetadataProperties;

foreach (KeyValuePair<string, JToken> entry in properties) {
    if (entry.Key.Contains("ImageWidth")) {
        width = (int)entry.Value;
    }

    if (entry.Key.Contains("ImageHeight")) {
        height = (int)entry.Value;
    }
}

MClient.Logger.Info("Width: " + width + " Height: " + height);

Orientation? orientation = null;
if (width > height) {
    orientation = Orientation.Landscape;
}
else if (width < height) {
    orientation = Orientation.Portrait;
}
else if (width != 0 && width == height) {
    orientation = Orientation.Square;
}

MClient.Logger.Info("Orientation: " + orientation.ToString());

if(orientation != null) {
    var orientationParents = entity.GetRelation<IChildToManyParentsRelation>("Orientation").Parents;
    orientationParents.Add((long)orientation);

    await MClient.Entities.SaveAsync(Context.Asset).ConfigureAwait(false);
}