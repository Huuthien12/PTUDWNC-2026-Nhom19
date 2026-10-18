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
          {safeThumbnailSource && <source media="(max-width: 400px)" srcSet={safeThumbnailSource} />}
          {safeMediumSource && <source media="(max-width: 1000px)" srcSet={safeMediumSource} />}
          <Image src={safeSource} alt={title} fill sizes="(max-width: 640px) 100vw, (max-width: 1024px) 50vw, 33vw"
            unoptimized onError={() => setFailedSource(safeSource)} />
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
