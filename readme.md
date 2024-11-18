<!-- Prompt Template based on https://github.com/othneildrew/Best-README-Template-->
<a id="readme-top"></a>

[![Issues][issues-shield]][issues-url]
[![Apache 2.0 License][license-shield]][license-url]



<!-- PROJECT LOGO -->
<br />
<h3 align="center">Dell Validated Designs for AI PCs<br /><strong>Image Search</strong></h3>

<p>
    Dell <a href="https://www.dell.com/en-us/lp/dt/workloads-validated-designs">Validated Designs</a> for AI PCs 
    are open-source reference guides that streamline development of AI applications meant to run on Dell AI PCs with NPU (neural processing unit) technology. 
    <br /><br />
    This project showcases <strong>Offline Image Search</strong> capabilities with some multimodal models by using OpenAI CLIP and OpenAI Whisper running on AI PC NPUs.
</p>


<!-- TABLE OF CONTENTS -->
<details>
  <summary>Table of Contents</summary>
  <ol>
    <li>
      <a href="#about-the-project">About The Project</a>
      <ul>
        <li><a href="#built-with">Built With</a></li>
      </ul>
    </li>
    <li>
      <a href="#getting-started">Getting Started</a>
      <ul>
        <li><a href="#validated-configurations">Validated Configurations</a></li>
        <li><a href="#software-prerequisites">Software Prerequesites</a></li>
        <li><a href="#installation">Installation</a></li>
      </ul>
    </li>
    <li><a href="#usage">Usage</a></li>
    <li><a href="#license">License</a></li>
  </ol>
</details>



<!-- ABOUT THE PROJECT -->
## About The Project

This simple reference application uses [CLIP](https://github.com/openai/CLIP) and [Whisper](https://github.com/openai/whisper) models from OpenAI running on both Qualcomm and Intel* AI PC silicon. It calculates encodings for all images in a directory with CLIP, then allows for use of spoken phrases (converted to text with Whisper) or text inputs to be encoded, compared to the image set encodings, and return the top matches, all on device.

\* Support for Intel AI PCs coming soon.

<p align="right">(<a href="#readme-top">back to top</a>)</p>


### Built With

* [ONNX](https://github.com/onnx)
* [ONNXRuntime](https://github.com/microsoft/onnxruntime)
* [MAUI](https://learn.microsoft.com/en-us/dotnet/maui)

<p align="right">(<a href="#readme-top">back to top</a>)</p>



<!-- GETTING STARTED -->
## Getting Started

This project requires AI PC hardware and software dependencies.

### Validated Configurations

This Dell Validated Design has been tested on Dell AI PCs with Qualcomm(R) Snapdragon(R) X Elite processors with at least 16 GB of RAM running Windows 11.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

### Software Prerequisites

Core Software Package:
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (Need MAUI, ARM, .NET Packages)

<p align="right">(<a href="#readme-top">back to top</a>)</p>

### Installation

1. (Recommended) Fork the repo so that you can save any changes that you make
1. Clone the repo
   ```sh
   git clone https://github.com/<your fork>/dvd-ai-pc-image-search.git
   cd dvd-ai-pc-image-search
   ```
1. Open the solution in Visual Studio
   ```sh
   devenv SemanticImageSearchAIPCT.sln
   ```
1. Set the processor architecture for your build and build the solution.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Usage

1. Run the application in Visual Studio

1. For initial use we recommend that you use a sample image dataset like [COCO (a validation subset)](https://cocodataset.org/#download) or [Fruits-360](https://www.kaggle.com/datasets/moltean/fruits). After downloading one locally, click on the `Import` button and select the folder containing the images to begin generating encodings. Progress is shown on the bottom bar of the application, with further information on the `Debug` tab.

1. Once the import is complete, click on the microphone button to start phrase detection or type in the text box (and press `Enter` to trigger search).

1. The application will retrieve the images that best match the supplied search term(s).

![Image Search DVD Demo Ingest on Qualcomm AI PC](https://github.com/dell/dvd-ai-pc-image-search/raw/main/assets/dvd-ai-pc-image-search-qualcomm-ingest.gif "Ingest Demo on Qualcomm AI PC")

![Image Search DVD Demo on Qualcomm AI PC](https://github.com/dell/dvd-ai-pc-image-search/raw/main/assets/dvd-ai-pc-image-search-qualcomm.gif "Demo on Qualcomm AI PC")

<p align="right">(<a href="#readme-top">back to top</a>)</p>


<!-- LICENSE -->
## License

Distributed under the Apache 2.0 License. See `LICENSE.txt` for more information.

<p align="right">(<a href="#readme-top">back to top</a>)</p>


<!-- AUTHORS -->
## Authors

Robert Hernandez, Parimi Satya, Mark Chen, Tyler Cox

<p align="right">(<a href="#readme-top">back to top</a>)</p>



<h3 align="center">
    <a href="https://dell.com/ai">Learn more about Dell AI Solutions and the Dell AI Factory »</a>
</h3>

<!-- MARKDOWN LINKS & IMAGES -->
<!-- https://www.markdownguide.org/basic-syntax/#reference-style-links -->
[contributors-shield]: https://img.shields.io/github/contributors/dell/dvd-ai-pc-image-search.svg?style=for-the-badge
[contributors-url]: https://github.com/dell/dvd-ai-pc-image-search/graphs/contributors
[forks-shield]: https://img.shields.io/github/forks/dell/dvd-ai-pc-image-search.svg?style=for-the-badge
[forks-url]: https://github.com/dell/dvd-ai-pc-image-search/network/members
[stars-shield]: https://img.shields.io/github/stars/dell/dvd-ai-pc-image-search.svg?style=for-the-badge
[stars-url]: https://github.com/dell/dvd-ai-pc-image-search/stargazers
[issues-shield]: https://img.shields.io/github/issues/dell/dvd-ai-pc-image-search.svg?style=for-the-badge
[issues-url]: https://github.com/dell/dvd-ai-pc-image-search/issues
[license-shield]: https://img.shields.io/github/license/dell/dvd-ai-pc-image-search.svg?style=for-the-badge
[license-url]: https://github.com/dell/dvd-ai-pc-image-search/blob/main/LICENSE.txt
[linkedin-shield]: https://img.shields.io/badge/-LinkedIn-black.svg?style=for-the-badge&logo=linkedin&colorB=555
[linkedin-url]: https://linkedin.com/in/linkedin_username
[product-screenshot]: images/screenshot.png
[Next.js]: https://img.shields.io/badge/next.js-000000?style=for-the-badge&logo=nextdotjs&logoColor=white
[Next-url]: https://nextjs.org/
[React.js]: https://img.shields.io/badge/React-20232A?style=for-the-badge&logo=react&logoColor=61DAFB
[React-url]: https://reactjs.org/
[Vue.js]: https://img.shields.io/badge/Vue.js-35495E?style=for-the-badge&logo=vuedotjs&logoColor=4FC08D
[Vue-url]: https://vuejs.org/
[Angular.io]: https://img.shields.io/badge/Angular-DD0031?style=for-the-badge&logo=angular&logoColor=white
[Angular-url]: https://angular.io/
[Svelte.dev]: https://img.shields.io/badge/Svelte-4A4A55?style=for-the-badge&logo=svelte&logoColor=FF3E00
[Svelte-url]: https://svelte.dev/
[Laravel.com]: https://img.shields.io/badge/Laravel-FF2D20?style=for-the-badge&logo=laravel&logoColor=white
[Laravel-url]: https://laravel.com
[Bootstrap.com]: https://img.shields.io/badge/Bootstrap-563D7C?style=for-the-badge&logo=bootstrap&logoColor=white
[Bootstrap-url]: https://getbootstrap.com
[JQuery.com]: https://img.shields.io/badge/jQuery-0769AD?style=for-the-badge&logo=jquery&logoColor=white
[JQuery-url]: https://jquery.com 
