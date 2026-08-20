import {
  CircleOff,
  Eye,
  FolderTree,
  LoaderCircle,
  Plus,
  Redo2,
  Search,
  Star,
  Trash2,
  Undo2,
  X,
  Zap,
} from "lucide-react";
import React, { useEffect, useRef, useState } from "react";
import * as Dialog from "@radix-ui/react-dialog";
import { useNavigate, useNavigation, useSearchParams } from "react-router-dom";
import type { MediaItem } from "../../lib/media-types";
import { librarySummaryTone } from "../../lib/media-status-presentation";
import {
  ApiRequestError,
  fetchJson,
  readValidationProblem,
  type CatalogueFacets,
  type CataloguePage,
  type CreateLibraryViewRequest,
  type LibraryViewItem,
  type MetadataProviderStatus,
  type MetadataSearchResult,
  type MovieListItem,
  type MovieWantedSummary,
  type SeriesListItem,
  type SeriesWantedSummary
} from "../../lib/api";
import { adaptMovieItems, adaptSeriesItems } from "../../lib/ui-adapters";
import { parseDisplayOptions } from "../../lib/library-filters";
import { ProgressiveGrid } from "./library-grid";
import {
  ControlRail,
  isQuickFilter,
  isSortField,
  type SavedFilterPreset,
} from "./library-control-rail";
import { LibraryTable } from "./library-table";
import { useDensity } from "../../lib/use-density";
import { useLibraryFilters } from "../../hooks/use-library-filters";
import { useBulkEdit, type BulkWorkflowOperation } from "../../hooks/use-bulk-edit";
import { authedFetch } from "../../lib/use-auth";
import { cn } from "../../lib/utils";
import { GlassTile, PageHero } from "../shell/page-hero";
import { EmptyState } from "../shell/empty-state";
import { LibraryGridSkeleton } from "../shell/skeleton";
import { toast } from "../shell/toaster";
import { Badge } from "../ui/badge";
import { Checkbox } from "../ui/checkbox";
import { Button } from "../ui/button";
import { ConfirmDialog } from "../ui/confirm-dialog";
import { Field } from "../ui/field";
import { Input } from "../ui/input";
import { Select } from "../ui/select";

type Variant = "movies" | "shows";
type CreateFormDraft = {
  title: string;
  year: string;
  imdbId: string;
  monitored: boolean;
  metadata: MetadataSearchResult | null;
};
function sameMetadataResult(left: MetadataSearchResult, right: MetadataSearchResult) {
  return left.provider === right.provider && left.providerId === right.providerId;
}

interface BulkRemovalResponse {
  successCount?: number;
  failureCount?: number;
  results?: Array<{
    movieId?: string;
    seriesId?: string;
    succeeded?: boolean;
    errorMessage?: string | null;
  }>;
}

export function LibraryView({
  isRouteLoading = false,
  metadataStatus,
  onReload,
  variant
}: {
  isRouteLoading?: boolean;
  metadataStatus?: MetadataProviderStatus | null;
  onReload?: () => void;
  variant: Variant;
}) {
  const navigate = useNavigate();
  const navigation = useNavigation();
  const [searchParams, setSearchParams] = useSearchParams();
  const { density } = useDensity();
  const [libraryItems, setLibraryItems] = useState<MediaItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [facets, setFacets] = useState<CatalogueFacets | null>(null);
  const [nextPageToken, setNextPageToken] = useState<string | null>(null);
  const [currentPageToken, setCurrentPageToken] = useState<string | null>(null);
  const [previousPageTokens, setPreviousPageTokens] = useState<Array<string | null>>([]);
  const [isCatalogueLoading, setIsCatalogueLoading] = useState(true);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [refreshVersion, setRefreshVersion] = useState(0);
  const {
    query, setQuery, quickFilter, setQuickFilter, view, setView, sortField, setSortField,
    sortDirection, setSortDirection, cardSize, displayOptions,
    savedPresets, setSavedPresets, newPresetName, setNewPresetName, isSavingPreset,
    setIsSavingPreset, changeSize, updateDisplayOptions, activeFilterCount
  } = useLibraryFilters(variant, searchParams.get("filter"));

  const {
    selectedIds, setSelectedIds, isBulkUpdating, setIsBulkUpdating, isBulkToolsOpen, setIsBulkToolsOpen,
    isRemovalConfirmationOpen, setIsRemovalConfirmationOpen, bulkOperation, setBulkOperation,
    bulkMonitored, setBulkMonitored, bulkQualityProfileId, setBulkQualityProfileId,
    bulkTargetLibraryId, setBulkTargetLibraryId, bulkTagsInput, setBulkTagsInput,
    bulkRenameTemplate, setBulkRenameTemplate, bulkRenamePreview, setBulkRenamePreview,
    bulkConfirming, setBulkConfirming, bulkError, setBulkError, bulkLibraries, bulkQualityProfiles,
    bulkOptionsLoading, undoStack, redoStack, runUndo, runRedo, openBulkTools,
    executeBulkToolsOperation, toggleSelectedId, toggleSelectAllVisible
  } = useBulkEdit({ libraryItems, setLibraryItems, variant });

  const [showCreate, setShowCreate] = useState(() => searchParams.get("add") === "true");
  const [isCreating, setIsCreating] = useState(false);
  const [createForm, setCreateForm] = useState(() => createInitialForm());
  const [metadataResults, setMetadataResults] = useState<MetadataSearchResult[]>([]);
  const [selectedMetadataResults, setSelectedMetadataResults] = useState<MetadataSearchResult[]>([]);
  const [isSearchingMetadata, setIsSearchingMetadata] = useState(false);
  const metadataSearchSequence = useRef(0);



  useEffect(() => {
    let cancelled = false;
    const timer = window.setTimeout(async () => {
      setIsCatalogueLoading(true);
      const params = new URLSearchParams({ pageSize: "100", sort: sortField, direction: sortDirection });
      if (query.trim()) params.set("search", query.trim());
      if (quickFilter !== "all") params.set("status", quickFilter);
      try {
        if (variant === "movies") {
          const [page, wanted] = await Promise.all([
            fetchJson<CataloguePage<MovieListItem>>(`/api/movies/page?${params}`),
            fetchJson<MovieWantedSummary>("/api/movies/wanted")
          ]);
          if (cancelled) return;
          setLibraryItems(adaptMovieItems(page.items, wanted));
          setTotalCount(page.totalCount ?? 0);
          setFacets(page.facets);
          setNextPageToken(page.nextPageToken);
          setCurrentPageToken(null);
          setPreviousPageTokens([]);
        } else {
          const [page, wanted] = await Promise.all([
            fetchJson<CataloguePage<SeriesListItem>>(`/api/series/page?${params}`),
            fetchJson<SeriesWantedSummary>("/api/series/wanted")
          ]);
          if (cancelled) return;
          setLibraryItems(adaptSeriesItems(page.items, wanted));
          setTotalCount(page.totalCount ?? 0);
          setFacets(page.facets);
          setNextPageToken(page.nextPageToken);
          setCurrentPageToken(null);
          setPreviousPageTokens([]);
        }
        setSelectedIds([]);
      } catch {
        if (!cancelled) {
          setLibraryItems([]);
          setTotalCount(0);
          setFacets(null);
          setNextPageToken(null);
          toast.error("Could not load the library.");
        }
      } finally {
        if (!cancelled) setIsCatalogueLoading(false);
      }
    }, 250);
    return () => { cancelled = true; window.clearTimeout(timer); };
  }, [query, quickFilter, refreshVersion, sortDirection, sortField, variant]);

  async function loadNextCataloguePage() {
    if (!nextPageToken || isLoadingMore) return;
    setIsLoadingMore(true);
    const params = new URLSearchParams({ pageSize: "100", sort: sortField, direction: sortDirection, pageToken: nextPageToken });
    if (query.trim()) params.set("search", query.trim());
    if (quickFilter !== "all") params.set("status", quickFilter);
    try {
      if (variant === "movies") {
        const [page, wanted] = await Promise.all([
          fetchJson<CataloguePage<MovieListItem>>(`/api/movies/page?${params}`),
          fetchJson<MovieWantedSummary>("/api/movies/wanted")
        ]);
        setLibraryItems(adaptMovieItems(page.items, wanted));
        setNextPageToken(page.nextPageToken);
        setPreviousPageTokens((current) => [...current, currentPageToken]);
        setCurrentPageToken(nextPageToken);
      } else {
        const [page, wanted] = await Promise.all([
          fetchJson<CataloguePage<SeriesListItem>>(`/api/series/page?${params}`),
          fetchJson<SeriesWantedSummary>("/api/series/wanted")
        ]);
        setLibraryItems(adaptSeriesItems(page.items, wanted));
        setNextPageToken(page.nextPageToken);
        setPreviousPageTokens((current) => [...current, currentPageToken]);
        setCurrentPageToken(nextPageToken);
      }
    } catch {
      toast.error("Could not load more titles.");
    } finally {
      setIsLoadingMore(false);
    }
  }

  async function loadPreviousCataloguePage() {
    if (previousPageTokens.length === 0 || isLoadingMore) return;
    const previousToken = previousPageTokens[previousPageTokens.length - 1] ?? null;

    setIsLoadingMore(true);
    const params = new URLSearchParams({ pageSize: "100", sort: sortField, direction: sortDirection });
    if (previousToken) params.set("pageToken", previousToken);
    if (query.trim()) params.set("search", query.trim());
    if (quickFilter !== "all") params.set("status", quickFilter);
    try {
      if (variant === "movies") {
        const [page, wanted] = await Promise.all([
          fetchJson<CataloguePage<MovieListItem>>(`/api/movies/page?${params}`),
          fetchJson<MovieWantedSummary>("/api/movies/wanted")
        ]);
        setLibraryItems(adaptMovieItems(page.items, wanted));
        setNextPageToken(page.nextPageToken);
      } else {
        const [page, wanted] = await Promise.all([
          fetchJson<CataloguePage<SeriesListItem>>(`/api/series/page?${params}`),
          fetchJson<SeriesWantedSummary>("/api/series/wanted")
        ]);
        setLibraryItems(adaptSeriesItems(page.items, wanted));
        setNextPageToken(page.nextPageToken);
      }
      setCurrentPageToken(previousToken);
      setPreviousPageTokens((current) => current.slice(0, -1));
    } catch {
      toast.error("Could not load the previous titles.");
    } finally {
      setIsLoadingMore(false);
    }
  }

  useEffect(() => {
    setCreateForm(createInitialForm());
  }, [variant]);

  useEffect(() => {
    if (searchParams.get("add") === "true") {
      setShowCreate(true);
    }
  }, [searchParams]);

  useEffect(() => {
    const query = createForm.title.trim();
    const selectedTitle = createForm.metadata?.title.trim().toLocaleLowerCase();
    if (
      !showCreate ||
      query.length < 2 ||
      metadataStatus?.isConfigured === false ||
      selectedTitle === query.toLocaleLowerCase()
    ) {
      return;
    }

    const timer = window.setTimeout(() => {
      void handleMetadataSearch({ silent: true });
    }, 350);

    return () => window.clearTimeout(timer);
  }, [
    createForm.metadata?.title,
    createForm.title,
    createForm.year,
    metadataStatus?.isConfigured,
    showCreate,
    variant
  ]);

  useEffect(() => {
    const primarySelection = selectedMetadataResults[0] ?? null;
    setCreateForm((current) => {
      if (!primarySelection) {
        if (current.metadata === null) return current;
        return { ...current, metadata: null };
      }

      if (current.metadata?.provider === primarySelection.provider && current.metadata.providerId === primarySelection.providerId) {
        return current;
      }

      return {
        ...current,
        title: primarySelection.title,
        year: primarySelection.year ? String(primarySelection.year) : current.year,
        imdbId: primarySelection.imdbId ?? current.imdbId,
        metadata: primarySelection
      };
    });
  }, [selectedMetadataResults]);


  useEffect(() => {
    let cancelled = false;

    async function loadLibraryViews() {
      try {
        const items = await fetchJson<LibraryViewItem[]>(`/api/library-views?variant=${variant}`);
        if (cancelled) {
          return;
        }

        setSavedPresets(
          items.map((item) => ({
            id: item.id,
            name: item.name,
            quickFilter: isQuickFilter(item.quickFilter) ? item.quickFilter : "all",
            sortField: isSortField(item.sortField) ? item.sortField : "title",
            sortDirection: item.sortDirection === "desc" ? "desc" : "asc",
            viewMode: item.viewMode === "list" ? "list" : "grid",
            cardSize: item.cardSize === "sm" || item.cardSize === "lg" ? item.cardSize : "md",
            displayOptions: parseDisplayOptions(item.displayOptionsJson)
          }))
        );
      } catch {
        if (!cancelled) {
          setSavedPresets([]);
        }
      }
    }

    void loadLibraryViews();

    return () => {
      cancelled = true;
    };
  }, [variant]);

  // The catalogue endpoint owns filtering and ordering. A browser-side pass
  // here would quietly make the answer incomplete after the first page.
  const filtered = libraryItems;

  const selectedCount = selectedIds.length;
  const monitoredCount = facets?.monitored ?? 0;
  const missingCount = facets?.missing ?? 0;
  const downloadingCount = 0;
  const downloadedCount = facets?.downloaded ?? 0;
  const totalSizeTb = (
    libraryItems.reduce((sum, item) => sum + (item.sizeGb ?? 0), 0) / 1024
  ).toFixed(1);

  const label = variant === "movies" ? "movies" : "TV shows";
  const singular = variant === "movies" ? "movie" : "TV show";
  const selectedMetadataCount = selectedMetadataResults.length;

  function openWorkspace(item: MediaItem) {
    navigate(item.type === "movie" ? `/movies/${item.id}` : `/tv/${item.id}`);
  }

  async function saveCurrentPreset() {
    const name = newPresetName.trim();
    if (!name) {
      toast.info("Give the custom filter a name first.");
      return;
    }

    setIsSavingPreset(true);
    try {
      const payload: CreateLibraryViewRequest = {
        variant,
        name,
        quickFilter,
        sortField,
        sortDirection,
        viewMode: view,
        cardSize,
        displayOptionsJson: JSON.stringify(displayOptions),
        // Rules were a browser-only filter over an incomplete page. Persist an
        // empty legacy value until the API exposes a server-side rule contract.
        rulesJson: "[]"
      };

      const created = await fetchJson<LibraryViewItem>("/api/library-views", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });

      setSavedPresets((current) => [
        ...current,
        {
          id: created.id,
          name: created.name,
          quickFilter: isQuickFilter(created.quickFilter) ? created.quickFilter : "all",
          sortField: isSortField(created.sortField) ? created.sortField : "title",
          sortDirection: created.sortDirection === "desc" ? "desc" : "asc",
          viewMode: created.viewMode === "list" ? "list" : "grid",
          cardSize: created.cardSize === "sm" || created.cardSize === "lg" ? created.cardSize : "md",
          displayOptions: parseDisplayOptions(created.displayOptionsJson)
        }
      ]);
      setNewPresetName("");
      toast.success("Custom filter saved");
    } catch {
      toast.error("Could not save this custom filter.");
    } finally {
      setIsSavingPreset(false);
    }
  }

  function applyPreset(preset: SavedFilterPreset) {
    setQuickFilter(preset.quickFilter);
    setSortField(preset.sortField);
    setSortDirection(preset.sortDirection);
    setView(preset.viewMode);
    changeSize(preset.cardSize);
    updateDisplayOptions(preset.displayOptions);
    toast.success(`Applied ${preset.name}`);
  }

  async function deletePreset(presetId: string) {
    try {
      const response = await authedFetch(`/api/library-views/${presetId}`, {
        method: "DELETE"
      });
      if (!response.ok) {
        throw new Error("delete-failed");
      }
      setSavedPresets((current) => current.filter((preset) => preset.id !== presetId));
    } catch {
      toast.error("Could not remove this custom filter.");
    }
  }

  async function handleMetadataSearch(options: { silent?: boolean } = {}) {
    const searchTitle = createForm.title.trim();
    if (!searchTitle) {
      toast.info(`Type a ${singular} name first.`);
      return;
    }

    if (metadataStatus && !metadataStatus.isConfigured) {
      toast.warning("Title matching is not available yet.", {
        description: "You can add the title manually now. Live matching will return when the Deluno server's metadata connection is available."
      });
      return;
    }

    const searchSequence = metadataSearchSequence.current + 1;
    metadataSearchSequence.current = searchSequence;
    setIsSearchingMetadata(true);
    try {
      const params = new URLSearchParams({
        mediaType: variant === "movies" ? "movies" : "tv",
        query: searchTitle
      });
      if (createForm.year) {
        params.set("year", createForm.year);
      }

      const results = await fetchJson<MetadataSearchResult[]>(`/api/metadata/search?${params.toString()}`);
      if (searchSequence !== metadataSearchSequence.current) {
        return;
      }

      setMetadataResults(results);
      if (results.length === 0 && !options.silent) {
        toast.info(metadataStatus?.isConfigured === false ? "Title matching is temporarily unavailable." : "No metadata matches found.");
      }
    } catch (error) {
      const message =
        error instanceof ApiRequestError
          ? error.message
          : "Metadata search failed.";
      if (!options.silent) {
        toast.error(message);
      }
    } finally {
      if (searchSequence === metadataSearchSequence.current) {
        setIsSearchingMetadata(false);
      }
    }
  }

  async function enrichMetadataResult(result: MetadataSearchResult) {
    try {
      const params = new URLSearchParams({
        mediaType: variant === "movies" ? "movies" : "tv",
        query: result.title,
        providerId: result.providerId
      });
      if (result.year) {
        params.set("year", String(result.year));
      }

      const details = await fetchJson<MetadataSearchResult[]>(`/api/metadata/search?${params.toString()}`);
      return details.find((item) => item.providerId === result.providerId) ?? result;
    } catch {
      // Keep card-level metadata available even if details fail.
      return result;
    }
  }

  function createDraftFromCurrentForm(): CreateFormDraft {
    return {
      title: createForm.title,
      year: createForm.year,
      imdbId: createForm.imdbId,
      monitored: createForm.monitored,
      metadata: createForm.metadata
    };
  }

  function createDraftFromMetadataSelection(result: MetadataSearchResult): CreateFormDraft {
    return {
      title: result.title,
      year: result.year ? String(result.year) : "",
      imdbId: result.imdbId ?? "",
      monitored: createForm.monitored,
      metadata: result
    };
  }

  async function submitCreateDraft(draft: CreateFormDraft) {
    const resolvedMetadata = draft.metadata ? await enrichMetadataResult(draft.metadata) : null;
    const response = await authedFetch(variant === "movies" ? "/api/movies" : "/api/series", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(
        variant === "movies"
          ? {
              title: draft.title,
              releaseYear: draft.year ? Number(draft.year) : null,
              imdbId: draft.imdbId || null,
              monitored: draft.monitored,
              ...metadataCreatePayload(resolvedMetadata)
            }
          : {
              title: draft.title,
              startYear: draft.year ? Number(draft.year) : null,
              imdbId: draft.imdbId || null,
              monitored: draft.monitored,
              ...metadataCreatePayload(resolvedMetadata)
            }
      )
    });
    if (!response.ok) {
      const problem = await readValidationProblem(response);
      throw new Error(problem?.title ?? `Could not add ${singular}.`);
    }
  }

  function applyMetadataResult(result: MetadataSearchResult) {
    setSelectedMetadataResults((currentSelection) => {
      const isSelected = currentSelection.some((item) => sameMetadataResult(item, result));
      if (isSelected) {
        return currentSelection.filter((item) => !sameMetadataResult(item, result));
      }

      void enrichMetadataResult(result).then((resolvedMetadata) => {
        setSelectedMetadataResults((current) =>
          current.some((item) => sameMetadataResult(item, result))
            ? current.map((item) => (sameMetadataResult(item, result) ? resolvedMetadata : item))
            : current
        );
      });

      return [...currentSelection, result];
    });
  }

  async function handleRemoveFromDeluno() {
    if (!selectedIds.length) return;

    setIsBulkUpdating(true);
    try {
      const response = await authedFetch(variant === "movies" ? "/api/movies/bulk" : "/api/series/bulk", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(
          variant === "movies"
            ? { movieIds: selectedIds, operation: "remove" }
            : { seriesIds: selectedIds, operation: "remove" }
        )
      });
      if (!response.ok) throw new Error("remove-from-deluno-failed");

      const result = await response.json() as BulkRemovalResponse;
      const removedIds = (result.results ?? [])
        .filter((item) => item.succeeded)
        .map((item) => item.movieId ?? item.seriesId)
        .filter((id): id is string => Boolean(id));
      const idsToRemove = removedIds.length ? removedIds : selectedIds;
      const failedCount = result.failureCount ?? Math.max(0, selectedIds.length - idsToRemove.length);

      setLibraryItems((current) => current.filter((item) => !idsToRemove.includes(item.id)));
      setSelectedIds((current) => current.filter((id) => !idsToRemove.includes(id)));
      setIsRemovalConfirmationOpen(false);
      toast.success(
        `${idsToRemove.length} ${singular}${idsToRemove.length === 1 ? "" : "s"} removed from Deluno. Imported files and download-client items were left alone.`
      );
      if (failedCount > 0) {
        toast.error(`${failedCount} title${failedCount === 1 ? "" : "s"} could not be removed.`);
      }
      onReload?.();
    } catch {
      toast.error("Could not remove the selected titles from Deluno.");
    } finally {
      setIsBulkUpdating(false);
    }
  }

  function openCreate() {
    setShowCreate(true);
  }

  function closeCreate() {
    metadataSearchSequence.current += 1;
    setIsSearchingMetadata(false);
    setSelectedMetadataResults([]);
    setShowCreate(false);
    if (searchParams.has("add")) {
      setSearchParams((current) => {
        const next = new URLSearchParams(current);
        next.delete("add");
        return next;
      }, { replace: true });
    }
  }

  async function handleCreate(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const selectedDrafts = selectedMetadataResults.map(createDraftFromMetadataSelection);
    if (!createForm.title.trim() && selectedDrafts.length === 0) {
      toast.info(`Type a ${singular} name first.`);
      return;
    }

    setIsCreating(true);
    try {
      if (selectedDrafts.length > 0) {
        const settled = await Promise.allSettled(selectedDrafts.map((draft) => submitCreateDraft(draft)));
        const successCount = settled.filter((result) => result.status === "fulfilled").length;
        const failureCount = settled.length - successCount;

        if (successCount > 0) {
          toast.success(`${successCount} ${successCount === 1 ? singular : label} added`);
        }

        if (failureCount === 0) {
          setCreateForm(createInitialForm());
          setMetadataResults([]);
          setSelectedMetadataResults([]);
          closeCreate();
          onReload?.();
          return;
        }

        setSelectedMetadataResults((current) =>
          current.filter((_, index) => settled[index]?.status === "rejected")
        );
        toast.error(`${failureCount} ${failureCount === 1 ? "title" : "titles"} could not be added.`);
        if (successCount > 0) {
          onReload?.();
        }
        return;
      }

      await submitCreateDraft(createDraftFromCurrentForm());
      toast.success(variant === "movies" ? "Movie added" : "TV show added");
      setCreateForm(createInitialForm());
      setMetadataResults([]);
      setSelectedMetadataResults([]);
      closeCreate();
      onReload?.();
    } catch (error) {
      const msg = error instanceof Error ? error.message : "Create failed.";
      toast.error(msg);
    } finally {
      setIsCreating(false);
    }
  }

  return (
    <>
      <section className="space-y-[var(--grid-gap)]">
        <div className="relative overflow-hidden rounded-2xl border border-hairline bg-card p-[var(--tile-pad)] shadow-card dark:border-white/[0.06]">
          <span
            aria-hidden
            className="pointer-events-none absolute inset-x-5 top-0 h-px rounded-full"
            style={{ background: "linear-gradient(90deg, transparent, hsl(var(--primary)/0.45), hsl(var(--primary-2)/0.28), transparent)" }}
          />
          <span aria-hidden className="pointer-events-none absolute -right-20 -top-28 h-64 w-64 rounded-full bg-primary/10 blur-3xl" />
          <div className="relative flex flex-col gap-[var(--grid-gap)] lg:flex-row lg:items-center lg:justify-between">
            <div className="min-w-0">
              <h2 className="font-display text-[length:var(--type-title-md)] font-semibold tracking-tight text-foreground">
                Browse and manage your {label}
              </h2>
              <p className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-[length:var(--type-body-sm)] text-muted-foreground">
                <span><span className="tabular font-semibold text-foreground">{totalCount.toLocaleString()}</span> total</span>
                <span className="text-muted-foreground/45">·</span>
                <span><span className={cn("tabular font-semibold", librarySummaryTone("availability", downloadedCount))}>{downloadedCount}</span> downloaded</span>
                <span className="text-muted-foreground/45">·</span>
                <span><span className="tabular font-semibold text-muted-foreground">{monitoredCount}</span> monitored</span>
                {missingCount > 0 ? (
                  <>
                    <span className="text-muted-foreground/45">·</span>
                    <span><span className="tabular font-semibold text-warning">{missingCount}</span> missing</span>
                  </>
                ) : null}
                {downloadingCount > 0 ? (
                  <>
                    <span className="text-muted-foreground/45">·</span>
                    <span><span className="tabular font-semibold text-info">{downloadingCount}</span> downloading</span>
                  </>
                ) : null}
              </p>
            </div>
            <div className="flex shrink-0 flex-wrap items-center gap-2">
              <Button className="gap-2" onClick={() => showCreate ? closeCreate() : openCreate()}>
                <Plus className="h-4 w-4" strokeWidth={2.5} />
                Add {singular}
              </Button>
              {missingCount > 0 ? (
                <Button variant="secondary" className="gap-2">
                  <Zap className="h-4 w-4" />
                  Hunt {missingCount} missing
                </Button>
              ) : null}
            </div>
          </div>
        </div>
        {/* ═══════ CINEMATIC HERO ═══════ */}
        <div className="hidden">
        <PageHero
          eyebrow={variant === "movies" ? "Movie library" : "TV library"}
          eyebrowIcon={
            <Star className="h-3 w-3 text-primary" />
          }
          title={
            <>
              Browse and manage your{" "}
              <span className="bg-gradient-to-r from-primary via-primary to-[hsl(var(--primary-2))] bg-clip-text text-transparent">
                {label}
              </span>
            </>
          }
          subtitle={
            <>
              <span className="font-semibold text-foreground">{totalCount.toLocaleString()} total titles</span>
              {" · "}
              <span className={cn("font-semibold", librarySummaryTone("availability", downloadedCount))}>{downloadedCount} downloaded</span>
              {missingCount > 0 ? (
                <>
                  {" · "}
                  <span className="font-semibold text-warning">{missingCount} missing</span>
                </>
              ) : null}
              {downloadingCount > 0 ? (
                <>
                  {" · "}
                  <span className="font-semibold text-info">{downloadingCount} downloading</span>
                </>
              ) : null}
            </>
          }
          size="sm"
          stats={[
            { label: "Total", value: totalCount.toString(), tone: "neutral" },
            { label: "Monitored", value: monitoredCount.toString(), tone: "primary" },
            { label: "Missing", value: missingCount.toString(), tone: missingCount > 0 ? "warn" : "neutral" },
            { label: "Library", value: `${totalSizeTb}TB`, tone: "neutral" }
          ]}
          actions={
            <>
              <Button className="gap-2" onClick={() => showCreate ? closeCreate() : openCreate()}>
                <Plus className="h-4 w-4" strokeWidth={2.5} />
                Add {singular}
              </Button>
              {missingCount > 0 ? (
                <Button variant="secondary" className="gap-2">
                  <Zap className="h-4 w-4" />
                  Hunt {missingCount} missing
                </Button>
              ) : null}
            </>
          }
        />
        </div>

        <Dialog.Root open={showCreate} onOpenChange={(open) => (open ? openCreate() : closeCreate())}>
          <Dialog.Portal>
            <Dialog.Overlay className="fixed inset-0 z-50 bg-black/55 backdrop-blur-[3px]" />
            <Dialog.Content className="fixed left-1/2 top-1/2 z-50 flex max-h-[min(88dvh,760px)] w-[calc(100%-2rem)] max-w-5xl -translate-x-1/2 -translate-y-1/2 flex-col overflow-hidden rounded-2xl border border-hairline bg-card shadow-2xl">
              <div className="flex items-start justify-between gap-[var(--grid-gap)] border-b border-hairline px-6 py-5">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <Dialog.Title className="font-display text-xl font-semibold tracking-tight text-foreground">Add {singular}</Dialog.Title>
                    <Badge variant={metadataStatus?.isConfigured ? "success" : "warning"}>
                      {metadataStatus?.isConfigured ? "Title matching ready" : "Manual entry"}
                    </Badge>
                  </div>
                  <Dialog.Description className="mt-1 text-sm text-muted-foreground">
                    Start typing, then pick matches to prefill details. Use Add at the bottom to create what you selected.
                  </Dialog.Description>
                </div>
                <Dialog.Close asChild>
                  <Button variant="ghost" size="icon" aria-label={`Close add ${singular}`} disabled={isCreating}>
                    <X className="h-4 w-4" />
                  </Button>
                </Dialog.Close>
              </div>

              <div className="min-h-0 flex-1 overflow-y-auto p-6">
                <form onSubmit={(event) => { event.preventDefault(); void handleMetadataSearch(); }}>
                  <label className="text-sm font-semibold text-foreground" htmlFor={`add-${variant}-title`}>What do you want to add?</label>
                  <div className="mt-2">
                    <Input
                      id={`add-${variant}-title`}
                      autoFocus
                      value={createForm.title}
                      onChange={(event) => {
                        metadataSearchSequence.current += 1;
                        setMetadataResults([]);
                        setSelectedMetadataResults([]);
                        setCreateForm((current) => ({ ...current, title: event.target.value, metadata: null }));
                      }}
                      placeholder={variant === "movies" ? "Search movies, for example Top Gun" : "Search TV shows, for example Severance"}
                    />
                  </div>
                  <p className="mt-2 text-xs text-muted-foreground">
                    <span className="inline-flex items-center gap-2">
                      {isSearchingMetadata ? <LoaderCircle className="h-3.5 w-3.5 animate-spin" /> : <Search className="h-3.5 w-3.5" />}
                      {metadataStatus?.isConfigured === false
                        ? "Metadata matching is currently unavailable."
                        : isSearchingMetadata
                          ? "Searching metadata..."
                          : "Matches auto-refresh as you type, or press Enter to refresh now."}
                    </span>
                  </p>
                </form>

                  {metadataStatus?.isConfigured === false ? (
                    <p className="mt-3 rounded-xl border border-warning/25 bg-warning/10 p-3 text-sm text-warning">
                      Title matching is temporarily unavailable. You can still add this title manually below.
                    </p>
                  ) : null}
                  {metadataResults.length > 0 ? (
                    <div className="mt-5">
                      <p className="text-sm font-semibold text-foreground">
                        Choose one or more matches to prefill (this does not add yet)
                      </p>
                      <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                    {metadataResults.slice(0, 6).map((result) => {
                      const isSelected = selectedMetadataResults.some((selected) => sameMetadataResult(selected, result));
                      return (
                            <button
                              key={`${result.provider}:${result.providerId}`}
                              type="button"
                              onClick={() => applyMetadataResult(result)}
                              className={cn(
                                "flex min-w-0 gap-3 rounded-xl border bg-surface-1 p-3 text-left transition hover:border-primary/45 hover:bg-primary/5",
                                isSelected ? "border-primary/70 bg-primary/10 ring-1 ring-primary/25" : "border-hairline"
                              )}
                              title={`Select ${result.title}`}
                        >
                              {result.posterUrl ? (
                                <img src={result.posterUrl} alt="" className="h-24 w-16 shrink-0 rounded-lg bg-muted object-cover" />
                              ) : (
                                <div className="flex h-24 w-16 shrink-0 items-center justify-center rounded-lg bg-muted text-[11px] text-muted-foreground">No art</div>
                              )}
                              <span className="min-w-0 self-center">
                                <span className="block truncate text-sm font-semibold text-foreground">{result.title}</span>
                                <span className="mt-1 block text-xs text-muted-foreground">{result.year ?? "Unknown year"} · TMDb</span>
                                {result.rating ? <span className="mt-2 block font-mono text-xs text-primary">{result.rating.toFixed(1)} rating</span> : null}
                                {isSelected ? (
                                  <span className="mt-1 inline-flex rounded-full bg-primary/15 px-2 py-0.5 text-[10px] font-semibold text-primary">Selected</span>
                                ) : (
                                  <span className="mt-1 block text-[10px] text-muted-foreground">Click to select</span>
                                )}
                              </span>
                            </button>
                          );
                        })}
                      </div>
                    </div>
                  ) : null}

                <details className="mt-5 rounded-xl border border-hairline bg-surface-1 px-4 py-3">
                  <summary className="cursor-pointer text-sm font-medium text-muted-foreground">Can’t find it? Add it manually</summary>
                  <div className="mt-3 grid gap-3 sm:grid-cols-2">
                    <Input
                      type="number"
                      value={createForm.year}
                      onChange={(event) => setCreateForm((current) => ({ ...current, year: event.target.value }))}
                      placeholder={variant === "movies" ? "Year (optional)" : "Start year (optional)"}
                    />
                    <Input
                      value={createForm.imdbId}
                      onChange={(event) => setCreateForm((current) => ({ ...current, imdbId: event.target.value }))}
                      placeholder="IMDb ID (optional)"
                    />
                  </div>
                </details>
              </div>

              <form onSubmit={handleCreate} className="flex flex-col gap-3 border-t border-hairline bg-surface-1/70 px-6 py-4 sm:flex-row sm:items-center sm:justify-between">
                <div className="inline-flex min-h-[1.25rem] items-center gap-2 text-sm text-muted-foreground">
                  <label className="inline-flex select-none items-center gap-2">
                    <Checkbox
                      checked={createForm.monitored}
                      onCheckedChange={(monitored) => setCreateForm((current) => ({ ...current, monitored }))}
                    />
                    Monitor and search automatically
                  </label>
                  {selectedMetadataCount > 0 ? (
                    <span className="inline-flex items-center gap-2 text-xs font-semibold text-primary">
                      <span className="h-1.5 w-1.5 rounded-full bg-primary" />
                      {selectedMetadataCount} {selectedMetadataCount === 1 ? singular : label} selected
                    </span>
                  ) : null}
                </div>
                <div className="flex gap-2">
                  <Dialog.Close asChild>
                    <Button type="button" variant="ghost" disabled={isCreating}>Cancel</Button>
                  </Dialog.Close>
                  <Button type="submit" disabled={isCreating || (!createForm.title.trim() && selectedMetadataCount === 0)} className="gap-2">
                    {isCreating ? <LoaderCircle className="h-4 w-4 animate-spin" /> : <Plus className="h-4 w-4" />}
                    {selectedMetadataCount > 0
                      ? selectedMetadataCount === 1
                        ? `Add selected ${singular}`
                        : `Add ${selectedMetadataCount} ${label}`
                      : `Add ${singular} manually`}
                  </Button>
                </div>
              </form>
            </Dialog.Content>
          </Dialog.Portal>
        </Dialog.Root>

        {/* ═══════ CONTROL RAIL ═══════ */}
        <ControlRail
          label={label}
          facets={facets}
          controls={{
            query, setQuery, quickFilter, setQuickFilter, sortField, setSortField,
            sortDirection, setSortDirection, view, setView, cardSize, changeSize,
            displayOptions, setDisplayOptions: updateDisplayOptions, savedPresets,
            newPresetName, setNewPresetName, isSavingPreset, saveCurrentPreset,
            applyPreset, deletePreset, activeFilterCount
          }}
        />

        {/* Results only occupy space when there is something to report. */}
        {totalCount > libraryItems.length ? (
          <div className="flex items-center justify-between gap-3">
            {totalCount > libraryItems.length ? (
              <p className="text-[length:var(--library-toolbar-size)] font-medium text-muted-foreground">
                Showing <span className="font-bold tabular text-foreground">{filtered.length}</span> loaded of {totalCount.toLocaleString()}
              </p>
          ) : (
            <span />
          )}

          {/* Right — premium select-all toggle */}
          <button
            type="button"
            onClick={toggleSelectAllVisible}
            className={cn(
              "group flex min-h-[var(--library-toolbar-height)] items-center gap-2 rounded-xl px-3 py-1.5 text-[length:var(--library-toolbar-size)] font-medium transition-all duration-200 select-none",
              selectedCount > 0
                ? "bg-primary/10 text-primary ring-1 ring-inset ring-primary/20 hover:bg-primary/15"
                : "text-muted-foreground hover:bg-muted/60 hover:text-foreground dark:hover:bg-white/[0.05]"
            )}
          >
            {/* Custom checkbox */}
            <span className={cn(
              "flex h-4 w-4 shrink-0 items-center justify-center rounded-[4px] border transition-all duration-200",
              filtered.length > 0 && filtered.every((i) => selectedIds.includes(i.id))
                ? "border-primary bg-primary text-primary-foreground shadow-[0_0_8px_hsl(var(--primary)/0.5)]"
                : selectedCount > 0
                  ? "border-primary/60 bg-primary/15"
                  : "border-hairline bg-background group-hover:border-primary/40 dark:bg-white/[0.04]"
            )}>
              {filtered.length > 0 && filtered.every((i) => selectedIds.includes(i.id)) ? (
                <svg width="9" height="7" viewBox="0 0 9 7" fill="none">
                  <path d="M1 3.5L3.5 6L8 1" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/>
                </svg>
              ) : selectedCount > 0 ? (
                /* Indeterminate dash */
                <span className="h-0.5 w-2 rounded-full bg-primary" />
              ) : null}
            </span>
            {selectedCount > 0 ? `${selectedCount} selected` : "Select all"}
          </button>
        </div>
        ) : null}

        {/* Action messages now surface through the global Toaster */}

        {/* ═══════ FLOATING SELECTION COMMAND BAR ═══════ */}
        {selectedCount > 0 ? (
          <div
            className={cn(
              "fixed z-50 mx-auto",
              "bottom-[calc(var(--mobile-tabbar-height)+16px)] md:bottom-8",
              "left-1/2 -translate-x-1/2",
              "animate-fade-in"
            )}
          >
            <div className={cn(
              "flex items-center overflow-hidden rounded-2xl",
              "border border-white/[0.1] dark:border-white/[0.08]",
              "bg-[hsl(226_24%_10%/0.97)] dark:bg-[hsl(226_24%_8%/0.98)]",
              "shadow-[0_24px_60px_hsl(0_0%_0%/0.45),0_8px_20px_hsl(0_0%_0%/0.3),inset_0_1px_0_hsl(0_0%_100%/0.06)]",
              "backdrop-blur-2xl"
            )}>
              <div className="flex items-center gap-2.5 border-r border-white/[0.07] px-4 py-3">
                <span className={cn(
                  "flex h-6 min-w-6 items-center justify-center rounded-full px-2",
                  "bg-gradient-to-br from-primary to-[hsl(var(--primary-2))]",
                  "text-[length:var(--library-badge-size)] font-bold text-primary-foreground",
                  "shadow-[0_2px_8px_hsl(var(--primary-deep)/0.5),inset_0_1px_0_hsl(0_0%_100%/0.2)]"
                )}>
                  {selectedCount}
                </span>
                <span className="whitespace-nowrap text-[length:var(--library-toolbar-size)] font-medium text-[hsl(var(--media-muted-foreground))]">
                  {selectedCount === 1 ? "item" : "items"} selected
                </span>
              </div>

              <div className="flex items-center gap-0.5 px-1.5 py-1.5">
                <BulkAction
                  label="Undo"
                  icon={<Undo2 className="h-3.5 w-3.5" />}
                  onClick={() => void runUndo()}
                  disabled={isBulkUpdating || undoStack.length === 0}
                />
                <BulkAction
                  label="Redo"
                  icon={<Redo2 className="h-3.5 w-3.5" />}
                  onClick={() => void runRedo()}
                  disabled={isBulkUpdating || redoStack.length === 0}
                />
                <BulkAction
                  label="Monitor"
                  icon={<Eye className="h-3.5 w-3.5" />}
                  onClick={() => openBulkTools("monitoring", true)}
                  disabled={isBulkUpdating}
                  loading={isBulkUpdating}
                  variant="primary"
                />
                <BulkAction
                  label="Search now"
                  icon={<Zap className="h-3.5 w-3.5" />}
                  onClick={() => openBulkTools("search")}
                  disabled={isBulkUpdating}
                />
                <BulkAction
                  label="Unmonitor"
                  icon={<CircleOff className="h-3.5 w-3.5" />}
                  onClick={() => openBulkTools("monitoring", false)}
                  disabled={isBulkUpdating}
                />
                <BulkAction
                  label="Remove"
                  icon={<Trash2 className="h-3.5 w-3.5" />}
                  onClick={() => setIsRemovalConfirmationOpen(true)}
                  disabled={isBulkUpdating}
                  variant="danger"
                />
                <BulkAction
                  label="Bulk tools"
                  icon={<FolderTree className="h-3.5 w-3.5" />}
                  onClick={() => openBulkTools("quality")}
                  disabled={isBulkUpdating}
                />
              </div>

              <div className="border-l border-white/[0.07] px-1.5 py-1.5">
                <button
                  type="button"
                  onClick={() => setSelectedIds([])}
                  className="flex min-h-[var(--library-toolbar-height)] items-center gap-1.5 rounded-xl px-3 text-[length:var(--library-toolbar-size)] font-medium text-[hsl(var(--media-muted-foreground)/0.65)] transition hover:bg-white/[0.06] hover:text-[hsl(var(--media-foreground))]"
                  aria-label="Clear selection"
                >
                  Clear
                  <kbd className="rounded border border-white/10 bg-white/[0.05] px-1 font-mono text-[length:var(--library-badge-size)] text-[hsl(var(--media-muted-foreground)/0.5)]">Esc</kbd>
                </button>
              </div>
            </div>
          </div>
        ) : null}

        {/* ═══════ POSTER GRID or LIST ═══════ */}
        {(isRouteLoading || navigation.state !== "idle") && libraryItems.length === 0 ? (
          <GlassTile className="p-[var(--tile-pad)]">
            <LibraryGridSkeleton count={20} />
          </GlassTile>
        ) : filtered.length === 0 ? (
          libraryItems.length === 0 ? (
            <EmptyState
              variant="library"
              title={`Your ${label} library is empty`}
              description={`Add your first ${singular} to start monitoring releases, running search, and building out your collection.`}
              action={
                <Button onClick={openCreate} className="gap-1.5">
                  <Plus className="h-4 w-4" strokeWidth={2.5} />
                  Add {singular}
                </Button>
              }
              learnMore={`Deluno will track up to 100,000 ${label} without breaking a sweat.`}
            />
          ) : (
            <EmptyState
              variant="search"
              title="Nothing matches"
              description={`Try clearing filters or broadening your search. Your library has ${libraryItems.length} total title${libraryItems.length === 1 ? "" : "s"}.`}
              action={
                <Button
                  variant="secondary"
                  onClick={() => {
                    setQuickFilter("all");
                    setQuery("");
                  }}
                >
                  Clear filters
                </Button>
              }
            />
          )
        ) : view === "grid" ? (
            <ProgressiveGrid
              items={filtered}
              cardSize={cardSize}
              density={density}
              displayOptions={displayOptions}
              selectedIds={selectedIds}
              keyBust={`${cardSize}-${quickFilter}-${query}-${sortField}-${sortDirection}-${displayOptions.showMeta}-${displayOptions.showStatusPill}-${displayOptions.showQualityBadge}-${displayOptions.showRating}`}
              onSelect={openWorkspace}
              onToggle={toggleSelectedId}
              onEndReached={() => undefined}
            />
        ) : (
          <GlassTile className="p-0">
            <LibraryTable
              items={filtered}
              selectedIds={selectedIds}
              onSelect={openWorkspace}
              onToggle={toggleSelectedId}
              onToggleAll={toggleSelectAllVisible}
              allSelected={filtered.length > 0 && filtered.every((item) => selectedIds.includes(item.id))}
              someSelected={selectedCount > 0 && !filtered.every((item) => selectedIds.includes(item.id))}
              onEndReached={() => undefined}
            />
          </GlassTile>
        )}
        {libraryItems.length > 0 && (previousPageTokens.length > 0 || nextPageToken) ? (
          <div className="flex items-center justify-between gap-3 border-t border-hairline pt-3">
            <Button
              type="button"
              variant="outline"
              disabled={previousPageTokens.length === 0 || isLoadingMore}
              onClick={() => void loadPreviousCataloguePage()}
            >
              Previous 100
            </Button>
            <p className="text-sm text-muted-foreground">Only this page is kept in memory.</p>
            <Button
              type="button"
              variant="outline"
              disabled={!nextPageToken || isLoadingMore}
              onClick={() => void loadNextCataloguePage()}
            >
              Next 100
            </Button>
          </div>
        ) : null}
      </section>

      {isBulkToolsOpen ? (
        <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/60 px-4 py-6 backdrop-blur-sm">
          <div className="w-full max-w-2xl space-y-[var(--page-gap)] rounded-2xl border border-hairline bg-card p-5 shadow-2xl">
            <div className="flex items-start justify-between gap-3">
              <div>
                <p className="text-[length:var(--type-caption)] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
                  Bulk workflow
                </p>
                <h3 className="font-display text-xl font-semibold text-foreground">
                  {selectedIds.length} title{selectedIds.length === 1 ? "" : "s"} selected
                </h3>
              </div>
              <Button
                type="button"
                variant="ghost"
                onClick={() => {
                  setIsBulkToolsOpen(false);
                  setBulkConfirming(false);
                  setBulkError(null);
                  setBulkRenamePreview([]);
                }}
                disabled={isBulkUpdating}
              >
                Close
              </Button>
            </div>

            <div className="grid gap-[var(--grid-gap)] md:grid-cols-2">
              <Field label="Operation" help="Choose the bulk action to run.">
                <Select
                  value={bulkOperation}
                  onChange={(event) => {
                    setBulkOperation(event.target.value as BulkWorkflowOperation);
                    setBulkConfirming(false);
                    setBulkError(null);
                    setBulkRenamePreview([]);
                  }}
                >
                  <option value="monitoring">Monitor or unmonitor</option>
                  <option value="quality">Set quality profile</option>
                  <option value="reassignLibrary">Assign library/root</option>
                  <option value="tags">Apply tags</option>
                  <option value="search">Search now</option>
                  <option value="renamePreview">Rename preview</option>
                </Select>
              </Field>

              {bulkOperation === "monitoring" ? (
                <Field label="Monitoring state" help="Apply monitored or unmonitored to the selection.">
                  <Select
                    value={bulkMonitored ? "true" : "false"}
                    onChange={(event) => setBulkMonitored(event.target.value === "true")}
                  >
                    <option value="true">Monitored</option>
                    <option value="false">Unmonitored</option>
                  </Select>
                </Field>
              ) : null}

              {bulkOperation === "quality" ? (
                <Field label="Quality profile" help="Set one quality profile for all selected titles.">
                  <Select
                    value={bulkQualityProfileId}
                    onChange={(event) => setBulkQualityProfileId(event.target.value)}
                  >
                    <option value="">Choose profile</option>
                    {bulkQualityProfiles.map((item) => (
                      <option key={item.id} value={item.id}>{item.name}</option>
                    ))}
                  </Select>
                </Field>
              ) : null}

              {bulkOperation === "reassignLibrary" ? (
                <Field label="Destination library" help="Reassign selected titles to a different library/root.">
                  <Select
                    value={bulkTargetLibraryId}
                    onChange={(event) => setBulkTargetLibraryId(event.target.value)}
                  >
                    <option value="">Choose library</option>
                    {bulkLibraries.map((item) => (
                      <option key={item.id} value={item.id}>{item.name}</option>
                    ))}
                  </Select>
                </Field>
              ) : null}

              {bulkOperation === "tags" ? (
                <Field label="Tags" help="Comma-separated tags to apply to all selected titles.">
                  <Input
                    value={bulkTagsInput}
                    onChange={(event) => setBulkTagsInput(event.target.value)}
                    placeholder="e.g. favorites, weekend, 4k"
                  />
                </Field>
              ) : null}

              {bulkOperation === "renamePreview" ? (
                <Field label="Template (optional)" help="Preview generated folder names before rename workflows.">
                  <Input
                    value={bulkRenameTemplate}
                    onChange={(event) => setBulkRenameTemplate(event.target.value)}
                    placeholder={variant === "movies" ? "{Movie Title} ({Release Year})" : "{Series Title} ({Series Year})"}
                  />
                </Field>
              ) : null}
            </div>

            {bulkConfirming && bulkOperation !== "renamePreview" ? (
              <div className="rounded-xl border border-amber-400/40 bg-amber-500/10 px-4 py-3 text-sm text-amber-100">
                Confirming will run this operation across {selectedIds.length} selected title{selectedIds.length === 1 ? "" : "s"}.
              </div>
            ) : null}

            {bulkError ? (
              <div className="rounded-xl border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
                {bulkError}
              </div>
            ) : null}

            {bulkOperation === "renamePreview" && bulkRenamePreview.length > 0 ? (
              <div className="max-h-72 overflow-auto rounded-xl border border-hairline bg-surface-1">
                <table className="min-w-full text-sm">
                  <thead className="sticky top-0 bg-surface-2 text-left">
                    <tr>
                      <th className="px-3 py-2">Title</th>
                      <th className="px-3 py-2">Proposed name</th>
                    </tr>
                  </thead>
                  <tbody>
                    {bulkRenamePreview.map((item) => (
                      <tr key={item.itemId} className="border-t border-hairline/70">
                        <td className="px-3 py-2 text-foreground">{item.title}</td>
                        <td className="px-3 py-2 font-mono text-xs text-muted-foreground">{item.proposedName}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : null}

            <div className="flex items-center justify-between gap-3">
              <div className="text-xs text-muted-foreground">
                {bulkOptionsLoading ? "Loading options..." : `Undo stack: ${undoStack.length} · Redo stack: ${redoStack.length}`}
              </div>
              <div className="flex items-center gap-2">
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => {
                    setIsBulkToolsOpen(false);
                    setBulkConfirming(false);
                    setBulkError(null);
                    setBulkRenamePreview([]);
                  }}
                  disabled={isBulkUpdating}
                >
                  Cancel
                </Button>
                <Button
                  type="button"
                  onClick={() => void executeBulkToolsOperation()}
                  disabled={isBulkUpdating || bulkOptionsLoading}
                >
                  {isBulkUpdating
                    ? "Running..."
                    : bulkOperation === "renamePreview"
                      ? "Run preview"
                      : bulkConfirming
                        ? "Confirm and run"
                        : "Review and continue"}
                </Button>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      <ConfirmDialog
        open={isRemovalConfirmationOpen}
        onOpenChange={setIsRemovalConfirmationOpen}
        title={`Remove ${selectedIds.length} ${singular}${selectedIds.length === 1 ? "" : "s"} from Deluno?`}
        description="This removes the selected catalog record and stops Deluno managing it. It does not delete imported media files or remove anything from your download client."
        confirmLabel="Remove from Deluno"
        busy={isBulkUpdating}
        onConfirm={() => void handleRemoveFromDeluno()}
      />

    </>
  );
}

function createInitialForm(): CreateFormDraft {
  return { title: "", year: "", imdbId: "", monitored: true, metadata: null as MetadataSearchResult | null };
}

function metadataCreatePayload(metadata: MetadataSearchResult | null) {
  if (!metadata) {
    return {};
  }

  return {
    metadataProvider: metadata.provider,
    metadataProviderId: metadata.providerId,
    originalTitle: metadata.originalTitle,
    overview: metadata.overview,
    posterUrl: metadata.posterUrl,
    backdropUrl: metadata.backdropUrl,
    rating: metadata.rating,
    genres: metadata.genres.join(", "),
    externalUrl: metadata.externalUrl,
    metadataJson: JSON.stringify(metadata)
  };
}

/* Premium bulk action button inside the inline selection bar */
function BulkAction({
  label,
  icon,
  onClick,
  disabled,
  loading,
  variant = "ghost"
}: {
  label: string;
  icon: React.ReactNode;
  onClick: () => void;
  disabled?: boolean;
  loading?: boolean;
  variant?: "ghost" | "primary" | "danger";
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      aria-label={label}
      className={cn(
        "flex min-h-[var(--library-toolbar-height)] items-center gap-1.5 rounded-xl px-3 text-[length:var(--library-toolbar-size)] font-medium transition-all duration-150 select-none",
        "disabled:opacity-40 disabled:cursor-not-allowed",
        variant === "primary"
          ? [
              "bg-gradient-to-br from-primary to-[hsl(var(--primary-2))] text-primary-foreground",
              "shadow-[0_2px_8px_hsl(var(--primary-deep)/0.4),inset_0_1px_0_hsl(0_0%_100%/0.15)]",
              "hover:brightness-110 active:scale-95"
            ].join(" ")
          : variant === "danger"
            ? "text-destructive hover:bg-destructive/10 hover:text-destructive active:bg-destructive/15"
            : [
              "text-[hsl(var(--media-muted-foreground))] hover:bg-white/[0.07] hover:text-[hsl(var(--media-foreground))] active:bg-white/[0.04]"
            ].join(" ")
      )}
    >
      {loading ? <LoaderCircle className="h-3.5 w-3.5 animate-spin" /> : icon}
      <span className="hidden sm:inline">{label}</span>
    </button>
  );
}

/* ══════════════════════════════════════════════════════
   CONTROL RAIL — premium floating bar with sliding indicator
══════════════════════════════════════════════════════ */
