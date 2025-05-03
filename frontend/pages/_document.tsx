import { Html, Head, Main, NextScript } from 'next/document'

export default function Document() {
  return (
    <Html lang="en">
      <Head>
        <link rel="icon" href="/favicon/favicon_32x32.png" sizes="32x32" />
        <link rel="icon" href="/favicon/favicon_64x64.png" sizes="64x64" />
        <link rel="icon" href="/favicon/favicon_128x128.png" sizes="128x128" />
      </Head>
      <body>
        <Main />
        <NextScript />
      </body>
    </Html>
  )
}
