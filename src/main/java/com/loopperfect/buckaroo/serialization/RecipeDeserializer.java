package com.loopperfect.buckaroo.serialization;

import com.google.common.base.Preconditions;
import com.google.common.collect.ImmutableMap;
import com.google.gson.*;
import com.loopperfect.buckaroo.*;

import java.lang.reflect.Type;
import java.util.stream.Collectors;

public final class RecipeDeserializer implements JsonDeserializer<Recipe> {

    @Override
    public Recipe deserialize(final JsonElement jsonElement, final Type type, final JsonDeserializationContext context) throws JsonParseException {

        Preconditions.checkNotNull(jsonElement);
        Preconditions.checkNotNull(type);
        Preconditions.checkNotNull(context);

        final JsonObject jsonObject = jsonElement.getAsJsonObject();

        final Identifier name = context.deserialize(jsonObject.get("name"), Identifier.class);
        final String url = jsonObject.get("url").getAsString();

        final JsonObject jsonObjectVersions = jsonObject.get("versions").getAsJsonObject();

        final ImmutableMap<SemanticVersion, RecipeVersion> versions = ImmutableMap.copyOf(
                jsonObjectVersions.entrySet().stream().collect(Collectors.toMap(
                        x -> context.deserialize(new JsonPrimitive(x.getKey()), SemanticVersion.class),
                        x -> context.deserialize(x.getValue(), RecipeVersion.class))));

        return Recipe.of(name, url, versions);
    }
}
