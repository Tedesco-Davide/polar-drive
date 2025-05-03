"use client";
import { useRouter } from "next/router";
import Image from "next/image";

export default function LanguageSwitcher() {
  const router = useRouter();

  const switchTo = (lng: string) => {
    router.push(router.pathname, router.asPath, { locale: lng });
  };

  return (
    <div className="xl:ml-[69px] flex space-x-4">
      <button onClick={() => switchTo("it")} aria-label="Italiano">
        <Image src="/icons/flag-it.svg" alt="IT" width={24} height={16} />
      </button>
      <button onClick={() => switchTo("en")} aria-label="English">
        <Image src="/icons/flag-en.svg" alt="EN" width={24} height={16} />
      </button>
    </div>
  );
}
