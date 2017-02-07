package com.loopperfect.buckaroo.serialization;

import com.google.common.base.Preconditions;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonSerializationContext;
import com.google.gson.JsonSerializer;
import com.loopperfect.buckaroo.RecipeVersion;

import java.lang.reflect.Type;

public final class RecipeVersionSerializer implements JsonSerializer<RecipeVersion> {

    @Override
    public JsonElement serialize(final RecipeVersion recipeVersion, final Type type, final JsonSerializationContext context) {

        Preconditions.checkNotNull(recipeVersion);
        Preconditions.checkNotNull(type);
        Preconditions.checkNotNull(context);

        final JsonObject jsonObject = new JsonObject();

        jsonObject.addProperty("url", recipeVersion.url);

        if (recipeVersion.buckUrl.isPresent()) {
            jsonObject.addProperty("buck-url", recipeVersion.buckUrl.get());
        }

        jsonObject.addProperty("target", recipeVersion.target);

        return jsonObject;
    }
}
