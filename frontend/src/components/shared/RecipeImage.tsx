"use client";

import Image from "next/image";
import { useEffect, useState, type CSSProperties } from "react";
import { getSession } from "@/services/auth-session";
import CulinaryIllustration from "./CulinaryIllustration";

export default function RecipeImage({ src, mediumSrc, thumbnailSrc, title, alt, className = "", style }: {
  src?: string | null;
  mediumSrc?: string | null;
  thumbnailSrc?: string | null;
  title: string;
  className?: string;
  alt?: string | null;
  style?: CSSProperties;
}) {
  const [failedSource, setFailedSource] = useState<string | null>(null);
  const [failedVariants, setFailedVariants] = useState<{ sourceKey: string; urls: string[] }>({ sourceKey: "", urls: [] });
  const [resolved, setResolved] = useState<Record<string, string>>({});
  const sourceKey = `${src}:${mediumSrc}:${thumbnailSrc}`;
  const safeSource = src && (/^https?:\/\//i.test(src) || /^\/(?!\/)/.test(src)) ? src : null;
  const safeThumbnailSource = thumbnailSrc && (/^https?:\/\//i.test(thumbnailSrc) || /^\/(?!\/)/.test(thumbnailSrc))
    ? thumbnailSrc
    : null;
  const safeMediumSource = mediumSrc && (/^https?:\/\//i.test(mediumSrc) || /^\/(?!\/)/.test(mediumSrc))
    ? mediumSrc
    : null;
  useEffect(() => {
    // One authenticated blob request per card prevents the three responsive variants
    // from exhausting the shared API rate limit during list navigation.
    const candidates = [safeSource].filter((value): value is string => Boolean(value));
    const protectedSources = candidates.filter((value) => value.includes("/recipes/"));
    if (protectedSources.length === 0) return;
    let disposed = false;
    const objectUrls: string[] = [];
    const session = getSession();
    void Promise.all(protectedSources.map(async (source) => {
      const response = await fetch(`/api/v1/images?source=${encodeURIComponent(source)}`, {
        headers: session ? { Authorization: `Bearer ${session.accessToken}` } : undefined,
        cache: "no-store",
      });
      if (!response.ok) return [source, ""] as const;
      const objectUrl = URL.createObjectURL(await response.blob());
      objectUrls.push(objectUrl);
      return [source, objectUrl] as const;
    })).then((entries) => {
      if (!disposed) setResolved(Object.fromEntries(entries.filter(([, value]) => value)));
    }).catch(() => { if (!disposed) setResolved({}); });
    return () => { disposed = true; objectUrls.forEach(URL.revokeObjectURL); };
  }, [safeSource]);
  const displaySource = safeSource && (safeSource.includes("/recipes/") ? resolved[safeSource] : safeSource);
  const displayThumbnailSource = safeThumbnailSource && (safeThumbnailSource.includes("/recipes/") ? resolved[safeThumbnailSource] : safeThumbnailSource);
  const displayMediumSource = safeMediumSource && (safeMediumSource.includes("/recipes/") ? resolved[safeMediumSource] : safeMediumSource);
  const failedVariantUrls = failedVariants.sourceKey === sourceKey ? failedVariants.urls : [];
  const isCurrentSource = (currentSrc: string, candidate: string) => {
    try { return new URL(candidate, globalThis.location?.href ?? "http://localhost").href === currentSrc; }
    catch { return candidate === currentSrc; }
  };
  return (
    <div className={`cb-recipe-visual ${className}`.trim()} style={style}>
      {safeSource?.includes("/recipes/") && !displaySource && failedSource !== safeSource ? <div className="cb-placeholder" role="status">Đang tải ảnh…</div> : displaySource && failedSource !== displaySource ? (
        <picture>
          {displayThumbnailSource && !failedVariantUrls.includes(displayThumbnailSource) &&
            <source media="(max-width: 400px)" srcSet={displayThumbnailSource} />}
          {displayMediumSource && !failedVariantUrls.includes(displayMediumSource) && <source srcSet={displayMediumSource} />}
          <Image src={displaySource} alt={alt ?? title} fill sizes="(max-width: 640px) 100vw, (max-width: 1024px) 50vw, 33vw"
            unoptimized onError={(event) => {
              const currentSrc = event.currentTarget.currentSrc;
              const failedVariant = [displayThumbnailSource, displayMediumSource]
                .find((candidate): candidate is string => Boolean(candidate && currentSrc && isCurrentSource(currentSrc, candidate)));
              if (failedVariant) {
                setFailedVariants((current) => ({
                  sourceKey,
                  urls: current.sourceKey === sourceKey
                    ? [...new Set([...current.urls, failedVariant])]
                    : [failedVariant],
                }));
              } else setFailedSource(displaySource);
            }} />
        </picture>
      ) : (
        <div className="cb-placeholder" role="img" aria-label={`Ảnh minh họa cho ${title}; chưa có ảnh món ăn`}>
          <CulinaryIllustration />
          <span>Góc bếp · Culinary Blog</span>
        </div>
      )}
    </div>
  );
}
