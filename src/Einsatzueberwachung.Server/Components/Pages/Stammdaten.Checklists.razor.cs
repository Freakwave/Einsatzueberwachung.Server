using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;

namespace Einsatzueberwachung.Server.Components.Pages;

public partial class Stammdaten
{
    private List<ChecklistTemplate> _checklistTemplates = new();
    private ChecklistTemplate _checklistTemplateDraft = new();
    private Guid? _editingChecklistTemplateId;
    private bool _showChecklistTemplateModal;

    private async Task RefreshChecklistTemplatesAsync()
    {
        _checklistTemplates = await MasterDataService.GetChecklistTemplatesAsync();
    }

    private void OpenCreateChecklistTemplateModal()
    {
        _checklistTemplateDraft = new ChecklistTemplate
        {
            Szenario = EinsatzSzenarioType.Mantrailer,
            Items = new List<ChecklistItemDefinition>()
        };
        _editingChecklistTemplateId = null;
        _showChecklistTemplateModal = true;
    }

    private void EditChecklistTemplate(Guid id)
    {
        var src = _checklistTemplates.FirstOrDefault(t => t.Id == id);
        if (src is null) return;

        _checklistTemplateDraft = new ChecklistTemplate
        {
            Id = src.Id,
            Szenario = src.Szenario,
            Name = src.Name,
            IsDefault = src.IsDefault,
            Items = src.Items.Select(it => new ChecklistItemDefinition
            {
                Id = it.Id,
                Label = it.Label,
                Type = it.Type,
                Choices = new List<string>(it.Choices),
                Required = it.Required
            }).ToList()
        };
        _editingChecklistTemplateId = id;
        _showChecklistTemplateModal = true;
    }

    private void CloseChecklistTemplateModal()
    {
        _showChecklistTemplateModal = false;
    }

    private void AddChecklistItem()
    {
        _checklistTemplateDraft.Items.Add(new ChecklistItemDefinition
        {
            Label = string.Empty,
            Type = ChecklistItemType.Bool
        });
    }

    private void RemoveChecklistItem(int index)
    {
        if (index < 0 || index >= _checklistTemplateDraft.Items.Count) return;
        _checklistTemplateDraft.Items.RemoveAt(index);
    }

    private void MoveChecklistItem(int index, int delta)
    {
        var target = index + delta;
        if (index < 0 || target < 0 || target >= _checklistTemplateDraft.Items.Count) return;
        var item = _checklistTemplateDraft.Items[index];
        _checklistTemplateDraft.Items.RemoveAt(index);
        _checklistTemplateDraft.Items.Insert(target, item);
    }

    private void SetChoices(ChecklistItemDefinition item, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            item.Choices = new List<string>();
            return;
        }
        item.Choices = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private async Task SaveChecklistTemplateAsync()
    {
        if (string.IsNullOrWhiteSpace(_checklistTemplateDraft.Name))
        {
            _status = "Bitte einen Namen vergeben.";
            return;
        }

        if (_editingChecklistTemplateId is null)
        {
            await MasterDataService.AddChecklistTemplateAsync(_checklistTemplateDraft);
        }
        else
        {
            await MasterDataService.UpdateChecklistTemplateAsync(_checklistTemplateDraft);
        }

        _showChecklistTemplateModal = false;
        await RefreshChecklistTemplatesAsync();
    }

    private async Task DeleteChecklistTemplateAsync(Guid id)
    {
        await MasterDataService.DeleteChecklistTemplateAsync(id);
        await RefreshChecklistTemplatesAsync();
    }
}
