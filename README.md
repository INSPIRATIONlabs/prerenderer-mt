# Prerenderer ( for StencilJs )

[![NuGet](https://buildstats.info/nuget/Prerenderer)][NugetUrl]
[![CodeFactor](https://www.codefactor.io/repository/github/inspirationlabs/prerenderer-mt/badge)][CodeFactorUrl]

[NugetUrl]: https://www.nuget.org/packages/Prerenderer/
[CodeFactorUrl]: https://www.codefactor.io/repository/github/inspirationlabs/prerenderer-mt

This prerenderer has mainly been written for [StencilJS](https://stenciljs.com). The main purpose of this project was to improve the rendertime for larger projects by using a multithreading approach. This is the reason why this project is a rewrite of https://github.com/inspirationlabs/prerenderer in C#. The main difference is that this prerenderer does not crawl the website and renders the page provided by a list of urls which should be rendered.

Some numbers:
1. Node implementation ~150 pages per minute
2. .NET core implementation ~1200 pages per minute

The system was a 16 core AWS machine.

The structure of the url list:

```js
{
  data: [
    {
      url: "/en/vivicity",
      published: true,
      indexed: true,
      followed: true
    },
    {
      url: "/de/vivicity",
      published: true,
      indexed: true,
      followed: true
    }
  ]
}
```

## Requirements

.NET core 2.1

## Install

```
dotnet tool install --global Prerenderer
```

Tested on Linux and Windows

## Usage

1. Copy the prerender.js to your projects src/assets directory.
2. Provide the urls.json file via an api endpoint or as part of your project. If it's part of your project you can put it also into src/assets.
3. Prerenderer.exe -u https://localhost:5000/assets/urls.json -s C:\Users\dominic.boettger\mystencilproject\www -o c:\Users\dominic.boettger\mystencilproject\output -i C:\Users\dominic.boettger\mystencilproject\src\assets\prerender.js -r 3

The prerenderer creates it's own webserver on port 5000 which should point to your projects build output directory ( this is by default the www directory in your stencil project).

### Available options

```text
  -t, --threads       Thread count

  -u, --urls          Required. http url to the list of urls in json format

  -c, --chromepath    Path to chromium binary

  -o, --output        Required. Path to output the data

  -s, --source        Required. Sourcepath to the build files of the js project

  -h, --host          The host with the source project

  -i, --injectFile    Path to a JS file to inject

  -r, --retry         (Default: 3) Times to retry the Rendering

  -b, --basePath      (Default: ) basePath for the rendering (only needed if it is not /)

  -m, --siteMapUrl    Domain url (http://www.mydomain.com) for the sitemap

  --help              Display this help screen.

  --version           Display version information.
  ```

### Development

If you want to change the sourcecode it's important to know that it's currently statically linked against this branch of pupeteer sharp https://github.com/dominicboettger/puppeteer-sharp/tree/features/executable-permissions

This is the case as Linux support was mandatory and there are currently missing unit tests for Linux. Puppeteer-sharp will soon be available with Linux support via nuget.

### Features

- [x] Basic prerendering
- [x] Stencil SSR and SSRC attributes
- [x] Retry support
- [x] BasePath to support projects which are running under a subPath
- [x] Output to directory
- [x] Threading based on CPU core count
- [x] Windows support
- [x] Linux support
- [x] sitemap.xml generation
- [ ] Server side rendering to support realtime requests and caching
- [ ] Unit tests
- [ ] CI pipeline

## Used in production by

- [Sixt MyDriver](https://www.mydriver.com)
- [Sixt GetARide](https://sixt.com/getaride/)