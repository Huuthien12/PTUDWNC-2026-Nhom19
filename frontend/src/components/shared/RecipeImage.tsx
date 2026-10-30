"use client";

import Image from "next/image";
import { useState, type CSSProperties } from "react";
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
  const sourceKey = `${src}:${mediumSrc}:${thumbnailSrc}`;
  const safeSource = src && (/^https?:\/\//i.test(src) || /^\/(?!\/)/.test(src)) ? src : null;
  const safeThumbnailSource = thumbnailSrc && (/^https?:\/\//i.test(thumbnailSrc) || /^\/(?!\/)/.test(thumbnailSrc))
    ? thumbnailSrc
    : null;
  const safeMediumSource = mediumSrc && (/^https?:\/\//i.test(mediumSrc) || /^\/(?!\/)/.test(mediumSrc))
    ? mediumSrc
    : null;
  const failedVariantUrls = failedVariants.sourceKey === sourceKey ? failedVariants.urls : [];
  const isCurrentSource = (currentSrc: string, candidate: string) => {
    try { return new URL(candidate, globalThis.location?.href ?? "http://localhost").href === currentSrc; }
    catch { return candidate === currentSrc; }
  };
  return (
    <div className={`cb-recipe-visual ${className}`.trim()} style={style}>
      {safeSource && failedSource !== safeSource ? (
        <picture>
          {safeThumbnailSource && !failedVariantUrls.includes(safeThumbnailSource) &&
            <source media="(max-width: 400px)" srcSet={safeThumbnailSource} />}
          {safeMediumSource && !failedVariantUrls.includes(safeMediumSource) && <source srcSet={safeMediumSource} />}
          <Image src={safeSource} alt={alt ?? title} fill sizes="(max-width: 640px) 100vw, (max-width: 1024px) 50vw, 33vw"
            unoptimized onError={(event) => {
              const currentSrc = event.currentTarget.currentSrc;
              const failedVariant = [safeThumbnailSource, safeMediumSource]
                .find((candidate): candidate is string => Boolean(candidate && currentSrc && isCurrentSource(currentSrc, candidate)));
              if (failedVariant) {
                setFailedVariants((current) => ({
                  sourceKey,
                  urls: current.sourceKey === sourceKey
                    ? [...new Set([...current.urls, failedVariant])]
                    : [failedVariant],
                }));
              } else setFailedSource(safeSource);
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
