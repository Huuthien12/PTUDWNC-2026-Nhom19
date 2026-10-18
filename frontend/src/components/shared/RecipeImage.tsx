"use client";

import Image from "next/image";
import { useState } from "react";
import CulinaryIllustration from "./CulinaryIllustration";

export default function RecipeImage({ src, mediumSrc, thumbnailSrc, title, className = "" }: {
  src?: string | null;
  mediumSrc?: string | null;
  thumbnailSrc?: string | null;
  title: string;
  className?: string;
}) {
  const [failedSource, setFailedSource] = useState<string | null>(null);
  const [failedVariants, setFailedVariants] = useState<string | null>(null);
  const sourceKey = `${src}:${mediumSrc}:${thumbnailSrc}`;
  const safeSource = src && (/^https?:\/\//i.test(src) || /^\/(?!\/)/.test(src)) ? src : null;
  const safeThumbnailSource = thumbnailSrc && (/^https?:\/\//i.test(thumbnailSrc) || /^\/(?!\/)/.test(thumbnailSrc))
    ? thumbnailSrc
    : null;
  const safeMediumSource = mediumSrc && (/^https?:\/\//i.test(mediumSrc) || /^\/(?!\/)/.test(mediumSrc))
    ? mediumSrc
    : null;
  return (
    <div className={`cb-recipe-visual ${className}`.trim()}>
      {safeSource && failedSource !== safeSource ? (
        <picture>
          {failedVariants !== sourceKey && safeThumbnailSource && <source media="(max-width: 400px)" srcSet={safeThumbnailSource} />}
          {failedVariants !== sourceKey && safeMediumSource && <source media="(min-width: 401px)" srcSet={safeMediumSource} />}
          <Image src={safeSource} alt={title} fill sizes="(max-width: 640px) 100vw, (max-width: 1024px) 50vw, 33vw"
            unoptimized onError={(event) => {
              if (failedVariants !== sourceKey && event.currentTarget.currentSrc &&
                  event.currentTarget.currentSrc !== event.currentTarget.src) setFailedVariants(sourceKey);
              else setFailedSource(safeSource);
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
