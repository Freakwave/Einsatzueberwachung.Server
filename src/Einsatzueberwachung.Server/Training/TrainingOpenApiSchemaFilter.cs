using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Einsatzueberwachung.Server.Training;

public sealed class TrainingOpenApiSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(CreateTrainingExerciseRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["externalReference"] = new OpenApiString("train-2026-04-25-001"),
                ["name"] = new OpenApiString("Flachsuche Waldabschnitt Nord"),
                ["scenario"] = new OpenApiString("Vermeintlich vermisste Person nach Nachtwanderung"),
                ["location"] = new OpenApiString("Musterwald, Sektor Nord"),
                ["plannedStartUtc"] = new OpenApiString("2026-04-25T08:30:00Z"),
                ["isTraining"] = new OpenApiBoolean(true),
                ["initiator"] = new OpenApiString("TrainingApp")
            };
            return;
        }

        if (context.Type == typeof(MirrorTrainingEventRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["type"] = new OpenApiString("lage"),
                ["text"] = new OpenApiString("Team 2 meldet Sichtung eines Kleidungsstuecks."),
                ["occurredAtUtc"] = new OpenApiString("2026-04-25T09:12:00Z"),
                ["isTraining"] = new OpenApiBoolean(true),
                ["sourceSystem"] = new OpenApiString("TrainingApp"),
                ["sourceUser"] = new OpenApiString("uebungsleiter")
            };
            return;
        }

        if (context.Type == typeof(MirrorTrainingDecisionRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["category"] = new OpenApiString("taktik"),
                ["decision"] = new OpenApiString("Abschnitt Ost mit Drohne aufklaeren"),
                ["rationale"] = new OpenApiString("Schnellere Sicht auf unwegsames Gelaende"),
                ["occurredAtUtc"] = new OpenApiString("2026-04-25T09:20:00Z"),
                ["isTraining"] = new OpenApiBoolean(true),
                ["sourceSystem"] = new OpenApiString("TrainingApp"),
                ["sourceUser"] = new OpenApiString("einsatzleitung")
            };
            return;
        }

        if (context.Type == typeof(CompleteTrainingExerciseRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["summary"] = new OpenApiString("Uebung erfolgreich abgeschlossen, Suchkette stabil."),
                ["completedAtUtc"] = new OpenApiString("2026-04-25T11:05:00Z"),
                ["isTraining"] = new OpenApiBoolean(true),
                ["sourceSystem"] = new OpenApiString("TrainingApp"),
                ["sourceUser"] = new OpenApiString("uebungsleitung")
            };
            return;
        }

        if (context.Type == typeof(SubmitTrainingReportRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["title"] = new OpenApiString("Uebungsbericht Flachsuche Nord"),
                ["content"] = new OpenApiString("Positive Teamkommunikation, Verbesserungspotential bei Funkdisziplin."),
                ["reportedAtUtc"] = new OpenApiString("2026-04-25T11:30:00Z"),
                ["isTraining"] = new OpenApiBoolean(true),
                ["sourceSystem"] = new OpenApiString("TrainingApp"),
                ["sourceUser"] = new OpenApiString("auswerter")
            };
        }
    }
}
