/**
 * Media Management: Media Naming, Import Policy, and Processing Workflow — one module.
 *
 *   File handling  → page form: Folder naming · PageFooter
 *   Processing     → ListCard of libraries → drawer (workflow per library),
 *                    plus optional completion callbacks as their own list.
 *
 * Contracts: PATCH /api/settings, PUT /api/libraries/{id}/workflow,
 * GET/POST/DELETE /api/integrations/processors/connections (+ /{id}/test).
 */
import { useEffect, useMemo, useState, type FormEvent } from "react";
import { useLoaderData, useLocation, useRevalidator } from "react-router-dom";
import { Loader2, Plus, Wifi } from "lucide-react";
import { Button } from "../components/ui/button";
import { Chip, type ChipProps } from "../components/ui/chip";
import { ConfirmDialog } from "../components/ui/confirm-dialog";
import { Drawer, DrawerFooter, DrawerSection, type DrawerSaveState } from "../components/ui/drawer";
import { Field, FieldRow } from "../components/ui/field";
import { Input } from "../components/ui/input";
import { ListCard, ListCell, ListEmpty, ListNameCell, ListRow, ListTable, LIST_TRACK } from "../components/ui/list-card";
import { PageFooter } from "../components/ui/page-footer";
import { PageToolbar, PageToolbarAction } from "../components/ui/page-toolbar";
import { PathInput } from "../components/ui/path-input";
import { Select } from "../components/ui/select";
import { SwitchRow } from "../components/ui/switch";
import { NamingFormatField, NamingPatternEditor, previewNamingFormat, type NamingFormatKind } from "../components/app/naming-format-field";
import { librarySetupNavItems } from "../components/app/settings-shell";
import { toast } from "../components/shell/toaster";
import { settingsOverviewLoader } from "./settings-overview-page";
import { fetchJson, type LibraryItem, type PlatformSettingsSnapshot, type ProcessorConnectionItem, type ProcessorConnectionTestResult, type QualityModelSnapshot, type QualityProfileItem } from "../lib/api";
import type { PlatformSettingsPatch } from "../lib/api/settings";
import { useApiMutation } from "../lib/use-api-mutation";
import { authedFetch } from "../lib/use-auth";
import { useUnsavedChanges } from "../hooks/use-unsaved-changes";

const TIMEOUT_OPTIONS = [
  { value: "60", label: "1 hour" },
  { value: "180", label: "3 hours" },
  { value: "360", label: "6 hours" },
  { value: "720", label: "12 hours" },
  { value: "1440", label: "24 hours" }
];
const FAILURE_OPTIONS = [
  { value: "block", label: "Stop and ask me" },
  { value: "manual-review", label: "Send to manual review" },
  { value: "import-original", label: "Import the original file" }
];

interface LoaderData {
  libraries: LibraryItem[];
  qualityProfiles: QualityProfileItem[];
  qualityModel: QualityModelSnapshot;
  settings: PlatformSettingsSnapshot;
  processorConnections: ProcessorConnectionItem[];
}

export async function settingsMediaManagementLoader(): Promise<LoaderData> {
  const [overview, processorConnections, qualityModel] = await Promise.all([
    settingsOverviewLoader(),
    fetchJson<ProcessorConnectionItem[]>("/api/integrations/processors/connections"),
    fetchJson<QualityModelSnapshot>("/api/quality-model")
  ]);
  return { ...overview, processorConnections, qualityModel };
}

export function SettingsMediaManagementPage() {
  const location = useLocation();
  if (location.pathname.startsWith("/settings/import-policy")) return <ImportPolicyPage />;
  return location.pathname.startsWith("/settings/processing") ? <ProcessingWorkflowPage /> : <FileHandlingPage />;
}

/* ==================================================== file handling */

interface CustomPatternDrawerState {
  kind: NamingFormatKind;
  label: string;
  placeholder: string;
  value: string;
  previousValue: string;
}

function customPatternMeta(kind: NamingFormatKind) {
  if (kind === "movie-folder") return { label: "Movie folders", placeholder: "{Movie Title} ({Release Year})" };
  if (kind === "series-folder") return { label: "TV show folders", placeholder: "{Series Title} ({Series Year})" };
  if (kind === "episode-file") return { label: "Episode files", placeholder: "{Series Title} - S{Season:00}E{Episode:00} - {Episode Title}" };
  return { label: "Custom format", placeholder: "" };
}

function updateCustomPattern(settings: PlatformSettingsSnapshot, kind: NamingFormatKind, value: string) {
  if (kind === "movie-folder") return { ...settings, movieFolderFormat: value };
  if (kind === "series-folder") return { ...settings, seriesFolderFormat: value };
  if (kind === "episode-file") return { ...settings, episodeFileFormat: value };
  return settings;
}

function customPatternPreview(kind: NamingFormatKind, value: string, placeholder: string) {
  const preview = previewNamingFormat(value || placeholder);
  if (kind === "movie-folder") return `Movies\\${preview}`;
  if (kind === "series-folder") return `TV Shows\\${preview}`;
  return preview;
}

function FileHandlingPage() {
  const { settings } = useLoaderData() as LoaderData;
  const settingsMutation = useApiMutation<PlatformSettingsPatch, PlatformSettingsSnapshot>("/api/settings", "PATCH");
  const [saved, setSaved] = useState(settings);
  const [form, setForm] = useState(settings);
  const [customDrawer, setCustomDrawer] = useState<CustomPatternDrawerState | null>(null);
  const [saveState, setSaveState] = useState<Exclude<DrawerSaveState, "clean" | "dirty">>();
  const [message, setMessage] = useState<string | null>(null);

  const dirty = useMemo(() => JSON.stringify(form) !== JSON.stringify(saved), [form, saved]);
  const state: DrawerSaveState = saveState === "saving" ? "saving" : dirty ? "dirty" : saveState ?? "clean";
  const customDrawerDirty = customDrawer !== null && customDrawer.value !== customDrawer.previousValue;
  useUnsavedChanges(dirty);
  useEffect(() => {
    if (dirty && (saveState === "saved" || saveState === "error")) setSaveState(undefined);
  }, [dirty, saveState]);

  function handleCustomMode(kind: NamingFormatKind, active: boolean, draftValue = "", previousValue = "") {
    if (!active) return;
    const meta = customPatternMeta(kind);
    setCustomDrawer({ kind, ...meta, value: draftValue, previousValue });
  }

  function closeCustomDrawer(apply: boolean) {
    if (!customDrawer) return;
    setForm((current) => updateCustomPattern(current, customDrawer.kind, apply ? customDrawer.value : customDrawer.previousValue));
    setCustomDrawer(null);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (state === "saving") return;
    setSaveState("saving");
    try {
      const nextSettings = await settingsMutation.mutate({
        movieFolderFormat: form.movieFolderFormat,
        seriesFolderFormat: form.seriesFolderFormat,
        episodeFileFormat: form.episodeFileFormat,
        renameOnImport: form.renameOnImport,
        useHardlinks: form.useHardlinks,
        cleanupEmptyFolders: form.cleanupEmptyFolders,
        downloadsPath: form.downloadsPath ?? ""
      });
      setSaved(nextSettings);
      setForm(nextSettings);
      setSaveState("saved");
      setMessage("Saved just now");
    } catch (error) {
      setSaveState("error");
      setMessage(error instanceof Error ? error.message : "Could not save");
    }
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-[var(--page-gap)]" noValidate>
      <PageToolbar tabs={librarySetupNavItems} />

      <ListCard title="Naming" count="The titles people see in your library">
        <div className="grid md:grid-cols-[minmax(0,1.45fr)_minmax(22rem,0.8fr)]">
          <div className="divide-y divide-hairline">
            <div className="grid gap-[var(--grid-gap)] p-[var(--card-pad-x)]">
              <div className="grid content-start gap-1.5">
                <p className="text-[length:var(--type-body-sm)] font-medium leading-tight text-foreground">Movie folders</p>
                <p className="text-[length:var(--type-caption)] leading-snug text-muted-foreground">Used when Deluno creates or renames a movie folder.</p>
              </div>
              <NamingFormatField kind="movie-folder" value={form.movieFolderFormat} onChange={(value) => setForm((current) => ({ ...current, movieFolderFormat: value }))} onCustomModeChange={(active, draftValue, previousValue) => handleCustomMode("movie-folder", active, draftValue, previousValue)} placeholder="{Movie Title} ({Release Year})" showExamples={false} />
            </div>
            <div className="grid gap-[var(--grid-gap)] p-[var(--card-pad-x)] md:grid-cols-2">
              <div className="grid content-start gap-[var(--grid-gap)]">
                <div className="grid content-start gap-1.5">
                  <p className="text-[length:var(--type-body-sm)] font-medium leading-tight text-foreground">TV show folders</p>
                  <p className="text-[length:var(--type-caption)] leading-snug text-muted-foreground">Used when Deluno creates or renames a show folder.</p>
                </div>
                <NamingFormatField kind="series-folder" value={form.seriesFolderFormat} onChange={(value) => setForm((current) => ({ ...current, seriesFolderFormat: value }))} onCustomModeChange={(active, draftValue, previousValue) => handleCustomMode("series-folder", active, draftValue, previousValue)} placeholder="{Series Title} ({Series Year})" showExamples={false} />
              </div>
              <div className="grid content-start gap-[var(--grid-gap)]">
                <div className="grid content-start gap-1.5">
                  <p className="text-[length:var(--type-body-sm)] font-medium leading-tight text-foreground">Episode files</p>
                  <p className="text-[length:var(--type-caption)] leading-snug text-muted-foreground">Used when Deluno renames imported episode files.</p>
                </div>
                <NamingFormatField kind="episode-file" value={form.episodeFileFormat} onChange={(value) => setForm((current) => ({ ...current, episodeFileFormat: value }))} onCustomModeChange={(active, draftValue, previousValue) => handleCustomMode("episode-file", active, draftValue, previousValue)} placeholder="{Series Title} - S{Season:00}E{Episode:00} - {Episode Title}" showExamples={false} />
              </div>
            </div>
          </div>
          <aside className="border-t border-hairline bg-surface-1/20 p-[var(--card-pad-x)] lg:border-l lg:border-t-0">
            <div className="grid gap-[var(--grid-gap)]">
              <div className="grid gap-1">
                <p className="text-[length:var(--type-body-sm)] font-semibold leading-tight text-foreground">Live preview</p>
                <p className="text-[length:var(--type-caption)] leading-snug text-muted-foreground">See how Deluno will name new and imported media.</p>
              </div>
              <div className="grid gap-[var(--grid-gap)]">
                <div className="grid gap-[var(--grid-gap)] border-b border-hairline pb-[var(--grid-gap)] last:border-b-0 last:pb-0">
                  <NamingPreview label="Movie folder" value={`Movies\\${previewNamingFormat(form.movieFolderFormat || "{Movie Title} ({Release Year})")}`} />
                </div>
                <div className="grid gap-[var(--grid-gap)] border-b border-hairline pb-[var(--grid-gap)] last:border-b-0 last:pb-0">
                  <NamingPreview label="TV show folder" value={`TV Shows\\${previewNamingFormat(form.seriesFolderFormat || "{Series Title} ({Series Year})")}`} />
                </div>
                <div className="grid gap-[var(--grid-gap)] border-b border-hairline pb-[var(--grid-gap)] last:border-b-0 last:pb-0">
                  <NamingPreview label="Episode file" value={previewNamingFormat(form.episodeFileFormat || "{Series Title} - S{Season:00}E{Episode:00} - {Episode Title}")} />
                </div>
              </div>
            </div>
          </aside>
        </div>
      </ListCard>

      <PageFooter state={state} message={message} saveLabel="Save changes" onDiscard={() => { setForm(saved); setCustomDrawer(null); }} />

      <Drawer
        open={customDrawer !== null}
        onOpenChange={(open) => {
          if (!open) closeCustomDrawer(false);
        }}
        title={customDrawer ? `Custom ${customDrawer.label.toLowerCase()} pattern` : "Custom pattern"}
        description="Build a naming format with Deluno's tokens, then preview the result before applying it."
        footer={customDrawer ? <DrawerFooter state={customDrawerDirty ? "dirty" : "clean"} saveLabel="Apply pattern" saveType="button" onSave={() => closeCustomDrawer(true)} onCancel={() => closeCustomDrawer(false)} /> : null}
      >
        {customDrawer ? (
          <>
            <DrawerSection title="Format" aside={customDrawer.label}>
              <NamingPatternEditor kind={customDrawer.kind} value={customDrawer.value} onChange={(value) => setCustomDrawer((current) => (current ? { ...current, value } : current))} placeholder={customDrawer.placeholder} />
            </DrawerSection>
            <DrawerSection title="Preview">
              <NamingPreview label={customDrawer.label} value={customPatternPreview(customDrawer.kind, customDrawer.value, customDrawer.placeholder)} />
            </DrawerSection>
          </>
        ) : null}
      </Drawer>
    </form>
  );
}

function ImportPolicyPage() {
  const { qualityModel: loadedQualityModel, settings } = useLoaderData() as LoaderData;
  const settingsMutation = useApiMutation<PlatformSettingsPatch, PlatformSettingsSnapshot>("/api/settings", "PATCH");
  const [savedQualityModel, setSavedQualityModel] = useState(loadedQualityModel);
  const [qualityModel, setQualityModel] = useState(loadedQualityModel);
  const [saved, setSaved] = useState(settings);
  const [form, setForm] = useState(settings);
  const [saveState, setSaveState] = useState<Exclude<DrawerSaveState, "clean" | "dirty">>();
  const [message, setMessage] = useState<string | null>(null);

  const dirty = useMemo(() => JSON.stringify(form) !== JSON.stringify(saved) || JSON.stringify(qualityModel) !== JSON.stringify(savedQualityModel), [form, qualityModel, saved, savedQualityModel]);
  const state: DrawerSaveState = saveState === "saving" ? "saving" : dirty ? "dirty" : saveState ?? "clean";
  useUnsavedChanges(dirty);
  useEffect(() => {
    if (dirty && (saveState === "saved" || saveState === "error")) setSaveState(undefined);
  }, [dirty, saveState]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (state === "saving") return;
    setSaveState("saving");
    try {
      const [nextSettings, nextQualityModel] = await Promise.all([
        settingsMutation.mutate({
          renameOnImport: form.renameOnImport,
          useHardlinks: form.useHardlinks,
          cleanupEmptyFolders: form.cleanupEmptyFolders,
          downloadsPath: form.downloadsPath ?? ""
        }),
        fetchJson<QualityModelSnapshot>("/api/quality-model", {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ tiers: qualityModel.tiers, upgradeStop: qualityModel.upgradeStop })
        })
      ]);
      setSaved(nextSettings);
      setForm(nextSettings);
      setSavedQualityModel(nextQualityModel);
      setQualityModel(nextQualityModel);
      setSaveState("saved");
      setMessage("Saved just now");
    } catch (error) {
      setSaveState("error");
      setMessage(error instanceof Error ? error.message : "Could not save");
    }
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-[var(--page-gap)]" noValidate>
      <PageToolbar tabs={librarySetupNavItems} />

      <ListCard title="Import Policy" count="What happens when a download is ready">
        <ImportPolicyFields
          form={form}
          qualityModel={qualityModel}
          onFormChange={(update) => setForm((current) => update(current))}
          onStopWhenCutoffMetChange={(checked) => setQualityModel((current) => ({ ...current, upgradeStop: { ...current.upgradeStop, stopWhenCutoffMet: checked } }))}
        />
      </ListCard>

      <PageFooter state={state} message={message} saveLabel="Save Import Policy" onDiscard={() => { setForm(saved); setQualityModel(savedQualityModel); }} />
    </form>
  );
}

function ImportPolicyFields({
  form,
  qualityModel,
  onFormChange,
  onStopWhenCutoffMetChange
}: {
  form: PlatformSettingsSnapshot;
  qualityModel: QualityModelSnapshot;
  onFormChange: (update: (current: PlatformSettingsSnapshot) => PlatformSettingsSnapshot) => void;
  onStopWhenCutoffMetChange: (checked: boolean) => void;
}) {
  return (
    <div className="grid md:grid-cols-2">
      <div className="border-b border-hairline p-[var(--card-pad-x)] md:border-r">
        <SwitchRow label="Rename on import" description="Use the naming styles from Media Naming." checked={form.renameOnImport} onCheckedChange={(checked) => onFormChange((current) => ({ ...current, renameOnImport: checked }))} />
      </div>
      <div className="border-b border-hairline p-[var(--card-pad-x)]">
        <SwitchRow label="Use hardlinks" description="Keep seeding without a second full copy." checked={form.useHardlinks} onCheckedChange={(checked) => onFormChange((current) => ({ ...current, useHardlinks: checked }))} />
      </div>
      <div className="border-b border-hairline p-[var(--card-pad-x)] md:border-r md:border-b-0">
        <SwitchRow label="Clean up empty folders" description="Remove leftover folders after import." checked={form.cleanupEmptyFolders} onCheckedChange={(checked) => onFormChange((current) => ({ ...current, cleanupEmptyFolders: checked }))} />
      </div>
      <div className="border-b border-hairline p-[var(--card-pad-x)] md:border-b-0">
        <SwitchRow label="Stop upgrading when cutoff is met" description="Keep monitoring missing media and future episodes, but stop searching for a better release once the cutoff quality is reached." checked={qualityModel.upgradeStop.stopWhenCutoffMet} onCheckedChange={onStopWhenCutoffMetChange} />
      </div>
      <div className="border-t border-hairline p-[var(--card-pad-x)] md:col-span-2">
        <Field label="Default completed-file location" optional help="Fallback for manual imports and downloads not linked to a client.">
          <PathInput value={form.downloadsPath ?? ""} onChange={(value) => onFormChange((current) => ({ ...current, downloadsPath: value }))} browseTitle="Choose downloads folder" />
        </Field>
      </div>
    </div>
  );
}

function NamingPreview({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid gap-1.5">
      <span className="text-[length:var(--type-micro)] font-semibold uppercase tracking-[0.08em] text-muted-foreground/70">{label}</span>
      <code className="break-words rounded-md border border-hairline bg-surface-1 px-2.5 py-2 text-[length:var(--type-caption)] leading-snug text-foreground">{value}</code>
    </div>
  );
}

/* ============================================== processing workflow */

interface WorkflowForm {
  importWorkflow: string;
  processorName: string;
  processorOutputPath: string;
  processorTimeoutMinutes: string;
  processorFailureMode: string;
}
interface CallbackForm {
  name: string;
  provider: string;
  submissionUrl: string;
  authHeaderName: string;
  secret: string;
  isEnabled: boolean;
}
type ProcessingDrawer = { kind: "closed" } | { kind: "library"; id: string } | { kind: "callback" };

function ProcessingWorkflowPage() {
  const { libraries, processorConnections } = useLoaderData() as LoaderData;
  const revalidator = useRevalidator();

  const [drawer, setDrawer] = useState<ProcessingDrawer>({ kind: "closed" });
  const [workflow, setWorkflow] = useState<WorkflowForm>(() => emptyWorkflow());
  const [workflowInitial, setWorkflowInitial] = useState<WorkflowForm>(() => emptyWorkflow());
  const [callback, setCallback] = useState<CallbackForm>(emptyCallback);
  const [saveState, setSaveState] = useState<Exclude<DrawerSaveState, "clean" | "dirty">>();
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [confirmRemove, setConfirmRemove] = useState<ProcessorConnectionItem | null>(null);
  const [confirmDiscard, setConfirmDiscard] = useState(false);

  const editingLibrary = drawer.kind === "library" ? libraries.find((library) => library.id === drawer.id) ?? null : null;
  const dirty =
    drawer.kind === "library"
      ? JSON.stringify(workflow) !== JSON.stringify(workflowInitial)
      : drawer.kind === "callback"
        ? Boolean(callback.name.trim() || callback.submissionUrl.trim() || callback.secret.trim())
        : false;
  const footerState: DrawerSaveState = saveState === "saving" ? "saving" : dirty ? "dirty" : saveState ?? "clean";
  const blocker = useUnsavedChanges(dirty);
  useEffect(() => {
    if (dirty && (saveState === "saved" || saveState === "error")) setSaveState(undefined);
  }, [dirty, saveState]);

  function openLibrary(library: LibraryItem) {
    const next = workflowFrom(library);
    setDrawer({ kind: "library", id: library.id });
    setWorkflow(next);
    setWorkflowInitial(next);
    setSaveState(undefined);
  }
  function openCallback() {
    setDrawer({ kind: "callback" });
    setCallback(emptyCallback());
    setSaveState(undefined);
  }
  function closeDrawer() {
    setDrawer({ kind: "closed" });
    setConfirmDiscard(false);
  }
  function requestClose() {
    if (dirty) setConfirmDiscard(true);
    else closeDrawer();
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    setBusy("save");
    setSaveState("saving");
    try {
      if (drawer.kind === "library" && editingLibrary) {
        const response = await authedFetch(`/api/libraries/${editingLibrary.id}/workflow`, {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            importWorkflow: workflow.importWorkflow,
            processorName: workflow.processorName || null,
            processorOutputPath: workflow.processorOutputPath || null,
            processorTimeoutMinutes: Number(workflow.processorTimeoutMinutes || 360),
            processorFailureMode: workflow.processorFailureMode
          })
        });
        if (!response.ok) throw new Error("Import workflow could not be saved.");
        setWorkflowInitial(workflow);
        setMessage("Saved just now");
      } else if (drawer.kind === "callback") {
        const response = await authedFetch("/api/integrations/processors/connections", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(callback) });
        if (!response.ok) throw new Error("Callback could not be saved.");
        setCallback(emptyCallback());
        setMessage("Callback saved");
        closeDrawer();
      }
      setSaveState("saved");
      revalidator.revalidate();
    } catch (error) {
      setSaveState("error");
      setMessage(error instanceof Error ? error.message : "Could not save");
    } finally {
      setBusy(null);
    }
  }

  async function run(key: string, action: () => Promise<unknown>, success?: string) {
    setBusy(key);
    try {
      await action();
      if (success) toast.success(success);
      revalidator.revalidate();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Action failed.");
    } finally {
      setBusy(null);
    }
  }

  async function testCallback(connection: ProcessorConnectionItem) {
    await run(`test:${connection.id}`, async () => {
      const response = await authedFetch(`/api/integrations/processors/connections/${connection.id}/test`, { method: "POST" });
      if (!response.ok) throw new Error("Callback test failed.");
      const result = (await response.json()) as ProcessorConnectionTestResult;
      toast.success(`${connection.name}: ${result.message}`);
    });
  }
  async function removeCallback(connection: ProcessorConnectionItem) {
    await run(`remove:${connection.id}`, async () => {
      const response = await authedFetch(`/api/integrations/processors/connections/${connection.id}`, { method: "DELETE" });
      if (!response.ok && response.status !== 204) throw new Error("Callback could not be removed.");
    }, `${connection.name} removed`);
    setConfirmRemove(null);
  }

  const isRefined = workflow.importWorkflow === "refine-before-import";

  return (
    <div className="grid gap-[var(--page-gap)]">
      <PageToolbar tabs={librarySetupNavItems} actions={<PageToolbarAction onClick={openCallback}>New callback</PageToolbarAction>} />

      <ListCard title="Import workflow" count="Standard import, or wait for a processor to clean the file first">
        {libraries.length === 0 ? (
          <ListEmpty title="No libraries yet" description="Create a library first; each one chooses whether completed downloads import straight away or wait for a processor." />
        ) : (
          <ListTable columns={[{ label: "Library" }, { label: "Workflow" }, { label: "Processed folder", width: "minmax(0,1.4fr)" }, { label: "If it fails" }, { label: "Status", width: LIST_TRACK.status, mobile: true }]}>
            {libraries.map((library) => {
              const refined = (library.importWorkflow ?? "standard") === "refine-before-import";
              const ready = !refined || Boolean(library.processorOutputPath);
              return (
                <ListRow key={library.id} onClick={() => openLibrary(library)} selected={drawer.kind === "library" && drawer.id === library.id}>
                  <ListNameCell name={library.name} sub={library.mediaType === "tv" ? "TV shows" : "Movies"} />
                  <ListCell primary={refined ? "Process before import" : "Standard import"} secondary={refined ? library.processorName || "Watches the folder — no callback" : "Straight to the library"} />
                  <ListCell mono primary={refined ? library.processorOutputPath || <span className="font-sans text-warning">Not set</span> : <span className="font-sans text-muted-foreground">—</span>} secondary={refined ? `Waits up to ${Math.round((library.processorTimeoutMinutes || 360) / 60)} h` : undefined} />
                  <ListCell primary={refined ? FAILURE_OPTIONS.find((option) => option.value === library.processorFailureMode)?.label ?? library.processorFailureMode : <span className="text-muted-foreground">—</span>} />
                  <ListCell mobile>
                    <Chip tone={ready ? (refined ? "info" : "ok") : "warn"}>{refined ? (ready ? "Refined" : "Needs a folder") : "Standard"}</Chip>
                  </ListCell>
                </ListRow>
              );
            })}
          </ListTable>
        )}
      </ListCard>

      <ListCard title="Completion callbacks" count="Optional — only when your automation can tell Deluno a file is ready">
        {processorConnections.length === 0 ? (
          <ListEmpty
            title="No callback configured"
            description="This is the normal setup: Deluno watches the processed-files folder directly and never needs to call your processor, or be called by it."
            actions={<Button type="button" size="sm" variant="outline" onClick={openCallback}><Plus className="h-3.5 w-3.5" />New callback</Button>}
          />
        ) : (
          <ListTable columns={[{ label: "Name" }, { label: "Type" }, { label: "Notifies", width: "minmax(0,1.4fr)" }, { label: "Health", width: LIST_TRACK.status, mobile: true }, { label: "", width: "150px", srOnly: true, mobile: true }]} chevron={false}>
            {processorConnections.map((connection) => (
              <ListRow key={connection.id}>
                <ListNameCell name={connection.name} sub={connection.secretConfigured ? "Token saved" : "No token"} />
                <ListCell primary={connection.provider === "fileflows-webhook" ? "FileFlows webhook" : "Generic callback"} />
                <ListCell mono primary={connection.submissionUrl || "—"} />
                <ListCell mobile>
                  <Chip tone={healthTone(connection.healthStatus)}>{connection.healthStatus}</Chip>
                </ListCell>
                <ListCell mobile align="end">
                  <span className="flex justify-end gap-2">
                    <Button type="button" variant="outline" size="sm" className="h-7 px-2" disabled={busy !== null} onClick={() => void testCallback(connection)}>
                      {busy === `test:${connection.id}` ? <Loader2 className="h-3 w-3 animate-spin" /> : <Wifi className="h-3 w-3" />}
                      Test
                    </Button>
                    <Button type="button" variant="destructive" size="sm" className="h-7 px-2" disabled={busy !== null} onClick={() => setConfirmRemove(connection)}>
                      Remove
                    </Button>
                  </span>
                </ListCell>
              </ListRow>
            ))}
          </ListTable>
        )}
      </ListCard>

      <Drawer
        open={drawer.kind !== "closed"}
        onOpenChange={(open) => {
          if (!open) requestClose();
        }}
        title={drawer.kind === "callback" ? "New completion callback" : editingLibrary?.name ?? "Import workflow"}
        description={drawer.kind === "callback" ? "Let existing automation tell Deluno the moment a processed file is ready." : `Import workflow · ${editingLibrary?.rootPath ?? ""}`}
        onSubmit={handleSubmit}
        footer={<DrawerFooter state={footerState} message={message} saveLabel={drawer.kind === "callback" ? "Save callback" : "Save workflow"} onCancel={requestClose} disabled={busy !== null || (drawer.kind === "library" && isRefined && !workflow.processorOutputPath.trim())} />}
      >
        {drawer.kind === "library" ? (
          <>
            <DrawerSection title="Workflow">
              <Field label="When a download finishes" help={isRefined ? "Deluno waits for a cleaned file in the processed folder, matches it to the download, then imports and renames it." : "Completed downloads go straight through destination routing, import, rename and metadata refresh."}>
                <Select value={workflow.importWorkflow} onChange={(event) => setWorkflow((current) => ({ ...current, importWorkflow: event.target.value }))} options={[{ value: "standard", label: "Standard import" }, { value: "refine-before-import", label: "Process before import" }]} />
              </Field>
            </DrawerSection>
            {isRefined ? (
              <DrawerSection title="Processing">
                <Field label="Processed-files folder Deluno can see" help="Your processor writes cleaned files here. Deluno imports one only when it matches a waiting download.">
                  <PathInput value={workflow.processorOutputPath} onChange={(value) => setWorkflow((current) => ({ ...current, processorOutputPath: value }))} browseTitle="Choose processed-output folder" />
                </Field>
                <FieldRow>
                  <Field label="Wait up to" help="Then Deluno asks you to review.">
                    <Select value={workflow.processorTimeoutMinutes} onChange={(event) => setWorkflow((current) => ({ ...current, processorTimeoutMinutes: event.target.value }))} options={TIMEOUT_OPTIONS} />
                  </Field>
                  <Field label="If processing fails">
                    <Select value={workflow.processorFailureMode} onChange={(event) => setWorkflow((current) => ({ ...current, processorFailureMode: event.target.value }))} options={FAILURE_OPTIONS} />
                  </Field>
                </FieldRow>
                <Field label="Completion callback" optional help="Leave as watched-folder unless your automation can notify Deluno with the hand-off id.">
                  <Select value={workflow.processorName} onChange={(event) => setWorkflow((current) => ({ ...current, processorName: event.target.value }))} placeholder="No callback — watch the folder">
                    {processorConnections.map((connection) => (
                      <option key={connection.id} value={connection.name}>
                        {connection.name}
                        {connection.isEnabled ? "" : " (disabled)"}
                      </option>
                    ))}
                    {workflow.processorName && !processorConnections.some((connection) => connection.name === workflow.processorName) ? <option value={workflow.processorName}>{workflow.processorName} (existing)</option> : null}
                  </Select>
                </Field>
              </DrawerSection>
            ) : null}
          </>
        ) : null}

        {drawer.kind === "callback" ? (
          <>
            <DrawerSection title="Basics">
              <FieldRow>
                <Field label="Name" help="A name you'll recognise in Transfers.">
                  <Input value={callback.name} onChange={(event) => setCallback((current) => ({ ...current, name: event.target.value }))} placeholder="Processed media notifier" autoComplete="off" />
                </Field>
                <Field label="Type">
                  <Select value={callback.provider} onChange={(event) => setCallback((current) => ({ ...current, provider: event.target.value }))} options={[{ value: "generic-webhook", label: "Generic processor webhook" }, { value: "fileflows-webhook", label: "FileFlows webhook" }]} />
                </Field>
              </FieldRow>
              <Field label="Notification URL" help="Deluno posts here when a completed download is waiting; your automation calls back with the same hand-off id.">
                <Input value={callback.submissionUrl} onChange={(event) => setCallback((current) => ({ ...current, submissionUrl: event.target.value }))} placeholder="https://processor.example/webhooks/deluno" className="font-mono text-[length:var(--type-caption)]" autoComplete="off" spellCheck={false} />
              </Field>
            </DrawerSection>
            <DrawerSection title="Authentication">
              <FieldRow>
                <Field label="Access token" optional help="Stored encrypted and sent only to this processor.">
                  <Input type="password" value={callback.secret} onChange={(event) => setCallback((current) => ({ ...current, secret: event.target.value }))} autoComplete="new-password" />
                </Field>
                <Field label="Token header" help="Authorization sends a Bearer token; use X-Api-Key when required.">
                  <Input value={callback.authHeaderName} onChange={(event) => setCallback((current) => ({ ...current, authHeaderName: event.target.value }))} />
                </Field>
              </FieldRow>
              <SwitchRow label="Enabled" description="Disabled callbacks stay configured but are never called." checked={callback.isEnabled} onCheckedChange={(checked) => setCallback((current) => ({ ...current, isEnabled: checked }))} />
            </DrawerSection>
          </>
        ) : null}
      </Drawer>

      <ConfirmDialog
        open={confirmRemove !== null}
        onOpenChange={(open) => {
          if (!open) setConfirmRemove(null);
        }}
        title={`Remove “${confirmRemove?.name}”?`}
        description="Libraries pointing at it fall back to watching the processed folder until another callback is chosen."
        confirmLabel="Remove callback"
        busy={busy?.startsWith("remove:") ?? false}
        onConfirm={() => confirmRemove && void removeCallback(confirmRemove)}
      />
      <ConfirmDialog
        open={confirmDiscard || blocker.state === "blocked"}
        onOpenChange={(open) => {
          if (open) return;
          setConfirmDiscard(false);
          if (blocker.state === "blocked") blocker.reset();
        }}
        title="Discard unsaved changes?"
        description="Your edits haven't been saved."
        confirmLabel="Discard"
        onConfirm={() => {
          setConfirmDiscard(false);
          if (blocker.state === "blocked") {
            setDrawer({ kind: "closed" });
            blocker.proceed();
          } else {
            closeDrawer();
          }
        }}
      />
    </div>
  );
}

/* --------------------------------------------------------------- utils */

function emptyWorkflow(): WorkflowForm {
  return { importWorkflow: "standard", processorName: "", processorOutputPath: "", processorTimeoutMinutes: "360", processorFailureMode: "block" };
}
function workflowFrom(library: LibraryItem): WorkflowForm {
  return {
    importWorkflow: library.importWorkflow ?? "standard",
    processorName: library.processorName ?? "",
    processorOutputPath: library.processorOutputPath ?? "",
    processorTimeoutMinutes: String(library.processorTimeoutMinutes || 360),
    processorFailureMode: library.processorFailureMode ?? "block"
  };
}
function emptyCallback(): CallbackForm {
  return { name: "", provider: "generic-webhook", submissionUrl: "", authHeaderName: "Authorization", secret: "", isEnabled: true };
}
function healthTone(status: string): NonNullable<ChipProps["tone"]> {
  switch (status) {
    case "healthy":
      return "ok";
    case "degraded":
    case "untested":
      return "warn";
    case "unknown":
      return "muted";
    default:
      return "bad";
  }
}
