"use client";

import Image from "next/image";
import { useState } from "react";
import CulinaryIllustration from "./CulinaryIllustration";

export default function RecipeImage({ src, title }: { src?: string | null; title: string }) {
  const [failedSource, setFailedSource] = useState<string | null>(null);
  const safeSource = src && (/^https?:\/\//i.test(src) || /^\/(?!\/)/.test(src)) ? src : null;
  return (
    <div className="cb-recipe-visual">
      {safeSource && failedSource !== safeSource ? (
        <Image src={safeSource} alt={title} fill sizes="(max-width: 600px) 100vw, (max-width: 900px) 50vw, 33vw"
          unoptimized onError={() => setFailedSource(safeSource)} />
      ) : (
        <div className="cb-placeholder" role="img" aria-label={`Ảnh minh họa cho ${title}; chưa có ảnh món ăn`}>
          <CulinaryIllustration />
          <span>Góc bếp · Culinary Blog</span>
        </div>
      )}
    </div>
  );
}
