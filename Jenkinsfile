properties([
  parameters([

    choice(
      name: 'ENVIRONMENT',
      choices: ['DEV', 'QA', 'STAGING', 'PRODUCTION'],
      description: 'Target deployment environment'
    ),

    choice(
      name: 'ACTION',
      choices: ['DEPLOY', 'ROLLBACK'],
      description: 'Select action'
    ),

    string(
      name: 'BRANCH',
      defaultValue: 'main',
      description: 'Git branch to build — used only when ENVIRONMENT=DEV and ACTION=DEPLOY'
    ),

    [$class: 'CascadeChoiceParameter',
      name: 'ARTIFACT_BUILD',
      description: 'Select the build to deploy (shows all successful DEV builds)',
      choiceType: 'PT_SINGLE_SELECT',
      filterable: true,
      script: [
        $class: 'GroovyScript',
        script: [
          $class: 'SecureGroovyScript',
          sandbox: false,
          script: '''
            import jenkins.model.Jenkins

            // Reads directly from Jenkins — works regardless of where agents are
            def job = Jenkins.instance.getItemByFullName("Jenkins-only")

            if (job == null) {
              return ["ERROR: Job 'Jenkins-only' not found"]
            }

            def list = []

            job.getBuilds().each { build ->
              if (build.getResult()?.toString() == "SUCCESS") {
                def date = new java.text.SimpleDateFormat("yyyy-MM-dd HH:mm").format(build.getTime())
                def env   = build.getEnvironment()
                def branch = env?.get("GIT_BRANCH") ?: "unknown"
                // Strip remote prefix e.g. "origin/main" -> "main"
                branch = branch.replaceAll("^origin/", "")
                list << "Build_${build.getNumber()} | ${date} | ${branch}"
              }
            }

            return list.isEmpty() ? ["No successful builds yet — run a DEV build first"] : list
          '''
        ]
      ]
    ]

  ])
])

pipeline {

  agent { label 'windows-agent' }

  environment {
    GIT_REPO      = 'https://github.com/1MRCV/studentmanagemnt.git'
    ARTIFACT_ROOT = 'C:\\jenkins-artifacts'

    // IIS deployment paths
    DEV_PATH      = 'C:\\inetpub\\studentportal\\dev'
    QA_PATH       = 'C:\\inetpub\\studentportal\\qa'
    STAGING_PATH  = 'C:\\inetpub\\studentportal\\staging'
    PROD_PATH     = 'C:\\inetpub\\studentportal\\prod'

    // IIS site names
    DEV_SITE      = 'student-dev'
    QA_SITE       = 'student-qa'
    STAGING_SITE  = 'student-staging'
    PROD_SITE     = 'student-prod'

    // IIS app pool names
    DEV_POOL      = 'student-dev-pool'
    QA_POOL       = 'student-qa-pool'
    STAGING_POOL  = 'student-staging-pool'
    PROD_POOL     = 'student-prod-pool'

    // Ports
    DEV_PORT      = '8081'
    QA_PORT       = '8082'
    STAGING_PORT  = '8083'
    PROD_PORT     = '8084'
  }

  stages {

    // ─────────────────────────────────────────────
    // Label the build in Jenkins UI
    // ─────────────────────────────────────────────
    stage('Set Build Name') {
      steps {
        script {
          def date = new Date().format('yyyy-MM-dd')
          currentBuild.displayName = "#${env.BUILD_NUMBER} | ${params.ENVIRONMENT} | ${params.ACTION} | ${date}"
        }
      }
    }

    // ─────────────────────────────────────────────
    // Only clean + checkout when building DEV
    // ─────────────────────────────────────────────
    stage('Clean Workspace') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION       == 'DEPLOY' }
        }
      }
      steps {
        deleteDir()
      }
    }

    stage('Checkout Code') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION       == 'DEPLOY' }
        }
      }
      steps {
        // gitTool 'Git-Windows' tells Jenkins to use the Windows git.exe
        // Configure this in: Manage Jenkins > Global Tool Configuration > Git
        // Name: Git-Windows | Path: C:\Program Files\Git\cmd\git.exe
        checkout([
          $class: 'GitSCM',
          branches: [[name: "${params.BRANCH}"]],
          userRemoteConfigs: [[url: "${env.GIT_REPO}"]],
          gitTool: 'Git-Windows'
        ])
      }
    }

    // ─────────────────────────────────────────────
    // Build .NET app and store versioned artifact
    // Only runs for DEV deploys
    // ─────────────────────────────────────────────
    stage('Build & Create Artifact') {
      when {
        allOf {
          expression { params.ENVIRONMENT == 'DEV' }
          expression { params.ACTION       == 'DEPLOY' }
        }
      }
      steps {
        powershell 'dotnet restore'
        powershell 'dotnet build --configuration Release'
        powershell 'dotnet publish StudentPortal.Web/StudentPortal.Web.csproj -c Release -o publish'

        powershell '''
          $build    = $env:BUILD_NUMBER
          $date     = Get-Date -Format "yyyy-MM-dd"
          $dateTime = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
          $root     = "C:\\jenkins-artifacts"

          # Create root if missing
          if (!(Test-Path $root)) {
            New-Item -ItemType Directory -Path $root -Force | Out-Null
          }

          # Versioned folder: build_42_2026-03-12
          $folder = "$root\\build_${build}_$date"
          New-Item -ItemType Directory -Path $folder -Force | Out-Null

          # Zip the published output
          Compress-Archive -Path "publish\\*" -DestinationPath "$folder\\artifact.zip" -Force

          # Store metadata so team knows what's inside
          $commit = git rev-parse --short HEAD
          $branch = git rev-parse --abbrev-ref HEAD

          @"
Application : StudentPortal
Build       : $build
Branch      : $branch
Commit      : $commit
Date        : $dateTime
"@ | Out-File "$folder\\build-info.txt" -Encoding UTF8

          Write-Host "SUCCESS: Artifact created at $folder"
        '''
      }
    }

    // ─────────────────────────────────────────────
    // Deploy to IIS — runs for all environments
    // and for both DEPLOY and ROLLBACK actions
    // ─────────────────────────────────────────────
    stage('Deploy to IIS') {
      steps {
        powershell """
          Import-Module WebAdministration

          # ── Resolve which artifact to deploy ──────────────────────────
          \$selected = "${params.ARTIFACT_BUILD}"
          \$root     = "${env.ARTIFACT_ROOT}"

          if ([string]::IsNullOrWhiteSpace(\$selected) -or
              \$selected -like "No successful*" -or
              \$selected -like "ERROR:*") {
            # Fallback: pick latest artifact folder automatically
            Write-Host "No artifact selected — using latest available."
            \$folder = Get-ChildItem \$root -Directory |
                       Sort-Object LastWriteTime -Descending |
                       Select-Object -First 1
          } else {
            # Parse "Build_42 | 2026-03-12 06:22 | main"
            \$buildNum = \$selected.Split("|")[0].Replace("Build_","").Trim()
            \$folder   = Get-ChildItem \$root -Directory |
                         Where-Object { \$_.Name -like "build_\${buildNum}_*" } |
                         Select-Object -First 1
          }

          if (\$null -eq \$folder) {
            Write-Error "No artifact folder found. Run a DEV build first."
            exit 1
          }

          \$zip = Join-Path \$folder.FullName "artifact.zip"

          if (!(Test-Path \$zip)) {
            Write-Error "artifact.zip missing inside \$(\$folder.FullName)"
            exit 1
          }

          Write-Host "Deploying artifact: \$(\$folder.Name)"

          # ── Resolve IIS config for selected environment ────────────────
          switch ("${params.ENVIRONMENT}") {
            "DEV" {
              \$site = "${env.DEV_SITE}"; \$pool = "${env.DEV_POOL}"
              \$path = "${env.DEV_PATH}"; \$port = "${env.DEV_PORT}"
            }
            "QA" {
              \$site = "${env.QA_SITE}";  \$pool = "${env.QA_POOL}"
              \$path = "${env.QA_PATH}";  \$port = "${env.QA_PORT}"
            }
            "STAGING" {
              \$site = "${env.STAGING_SITE}"; \$pool = "${env.STAGING_POOL}"
              \$path = "${env.STAGING_PATH}"; \$port = "${env.STAGING_PORT}"
            }
            "PRODUCTION" {
              \$site = "${env.PROD_SITE}"; \$pool = "${env.PROD_POOL}"
              \$path = "${env.PROD_PATH}"; \$port = "${env.PROD_PORT}"
            }
          }

          # ── Create deploy folder if missing ───────────────────────────
          if (!(Test-Path \$path)) {
            New-Item -ItemType Directory -Path \$path -Force | Out-Null
          }

          # ── Create App Pool if missing ────────────────────────────────
          if (!(Test-Path "IIS:\\AppPools\\\$pool")) {
            New-WebAppPool -Name \$pool
            Set-ItemProperty "IIS:\\AppPools\\\$pool" managedRuntimeVersion ""
            Write-Host "Created App Pool: \$pool"
          }

          # ── Create IIS Site if missing ────────────────────────────────
          if (!(Test-Path "IIS:\\Sites\\\$site")) {
            New-Website -Name \$site -Port \$port -PhysicalPath \$path -ApplicationPool \$pool
            Write-Host "Created IIS site: \$site on port \$port"
          }

          # ── Stop pool, swap files, restart ────────────────────────────
          Stop-WebAppPool -Name \$pool -ErrorAction SilentlyContinue
          Start-Sleep -Seconds 2

          Remove-Item "\$path\\*" -Recurse -Force -ErrorAction SilentlyContinue

          Expand-Archive -Path \$zip -DestinationPath \$path -Force

          Start-WebAppPool -Name \$pool
          Start-Website    -Name \$site

          Write-Host "SUCCESS: Deployed \$(\$folder.Name) to \$site (port \$port)"
        """
      }
    }

    // ─────────────────────────────────────────────
    // Quick sanity check — is the app pool running?
    // ─────────────────────────────────────────────
    stage('Verify') {
      steps {
        powershell """
          Import-Module WebAdministration

          switch ("${params.ENVIRONMENT}") {
            "DEV"        { \$pool = "${env.DEV_POOL}" }
            "QA"         { \$pool = "${env.QA_POOL}" }
            "STAGING"    { \$pool = "${env.STAGING_POOL}" }
            "PRODUCTION" { \$pool = "${env.PROD_POOL}" }
          }

          \$state = (Get-WebAppPoolState -Name \$pool).Value
          Write-Host "App Pool '\$pool' state: \$state"

          if (\$state -ne "Started") {
            Write-Error "App pool is not running — deployment may have failed."
            exit 1
          }

          Write-Host "Verification passed."
        """
      }
    }

  }

  post {
    success {
      echo "Pipeline #${BUILD_NUMBER} completed — ${params.ENVIRONMENT} ${params.ACTION}"
    }
    failure {
      echo "Pipeline #${BUILD_NUMBER} FAILED — check logs above."
    }
  }

}
